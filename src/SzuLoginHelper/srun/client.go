package srun

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"
)

const (
	callback        = "callback"
	defaultProbeURL = "http://1.1.1.1/"
)

// PortalParameters are discovered from the captive-portal redirect. ACIP is
// used only as the TCP destination; Host remains the HTTPS SNI and Host value.
type PortalParameters struct {
	Host     string
	ACID     string
	ACIP     string
	SSID     string
	ClientIP string
}

type ClientOptions struct {
	// ProbeURLs are requested over plain HTTP without redirects. A captive
	// portal should answer with a 30x Location that contains its parameters.
	ProbeURLs []string
	// Fallback is intentionally opt-in. It is used only after discovery fails
	// and is never populated with a campus-specific default.
	Fallback *PortalParameters
}

type LoginResult struct {
	Message string
}

type Client struct {
	probeURLs       []string
	fallback        *PortalParameters
	discoveryClient *http.Client
}

func NewClient(options ...ClientOptions) *Client {
	configuration := ClientOptions{ProbeURLs: []string{defaultProbeURL}}
	if len(options) > 0 {
		configuration = options[0]
		if len(configuration.ProbeURLs) == 0 {
			configuration.ProbeURLs = []string{defaultProbeURL}
		}
	}

	probeURLs := make([]string, 0, len(configuration.ProbeURLs))
	for _, probeURL := range configuration.ProbeURLs {
		if strings.TrimSpace(probeURL) != "" {
			probeURLs = append(probeURLs, strings.TrimSpace(probeURL))
		}
	}
	if len(probeURLs) == 0 {
		probeURLs = []string{defaultProbeURL}
	}

	var fallback *PortalParameters
	if configuration.Fallback != nil {
		copy := *configuration.Fallback
		fallback = &copy
	}

	return &Client{
		probeURLs: probeURLs,
		fallback:  fallback,
		discoveryClient: &http.Client{
			Transport: &http.Transport{
				Proxy:                 nil,
				DialContext:           (&net.Dialer{Timeout: 10 * time.Second}).DialContext,
				TLSHandshakeTimeout:   10 * time.Second,
				ResponseHeaderTimeout: 10 * time.Second,
			},
			CheckRedirect: func(_ *http.Request, _ []*http.Request) error {
				return http.ErrUseLastResponse
			},
			Timeout: 15 * time.Second,
		},
	}
}

// NewFallbackPortal validates an explicitly configured final fallback. Callers
// must supply all values; there are deliberately no teaching-area defaults.
func NewFallbackPortal(host, acID, acIP, clientIP string) (PortalParameters, error) {
	return newPortalParameters(host, acID, acIP, "", clientIP)
}

// ParsePortalRedirect extracts only the values needed by SRun from a captive
// portal Location header. It accepts both common spellings of the AC address.
func ParsePortalRedirect(location string) (PortalParameters, error) {
	redirect, err := url.Parse(location)
	if err != nil || redirect.Hostname() == "" {
		return PortalParameters{}, errors.New("captive-portal redirect did not contain a portal host")
	}

	values := redirect.Query()
	acIP := firstValue(values, "ac-ip", "ac_ip")
	clientIP := firstValue(values, "ip", "user_ip", "wlanuserip", "wlan_user_ip", "uaddress")
	return newPortalParameters(redirect.Host, values.Get("ac_id"), acIP, values.Get("ssid"), clientIP)
}

func firstValue(values url.Values, keys ...string) string {
	for _, key := range keys {
		if value := strings.TrimSpace(values.Get(key)); value != "" {
			return value
		}
	}
	return ""
}

func newPortalParameters(host, acID, acIP, ssid, clientIP string) (PortalParameters, error) {
	parsedHost, err := url.Parse("https://" + strings.TrimSpace(host))
	if err != nil || parsedHost.Hostname() == "" {
		return PortalParameters{}, errors.New("captive-portal redirect did not contain a valid portal host")
	}

	parsedACID, err := strconv.Atoi(strings.TrimSpace(acID))
	if err != nil || parsedACID <= 0 {
		return PortalParameters{}, errors.New("captive-portal redirect did not contain a valid ac_id")
	}
	if net.ParseIP(strings.TrimSpace(acIP)) == nil {
		return PortalParameters{}, errors.New("captive-portal redirect did not contain a valid AC address")
	}
	if net.ParseIP(strings.TrimSpace(clientIP)) == nil {
		return PortalParameters{}, errors.New("captive-portal redirect did not contain a valid client address")
	}

	return PortalParameters{
		Host:     parsedHost.Host,
		ACID:     strconv.Itoa(parsedACID),
		ACIP:     strings.TrimSpace(acIP),
		SSID:     strings.TrimSpace(ssid),
		ClientIP: strings.TrimSpace(clientIP),
	}, nil
}

func (p PortalParameters) endpoint(path string, parameters url.Values) string {
	return (&url.URL{
		Scheme:   "https",
		Host:     p.Host,
		Path:     path,
		RawQuery: parameters.Encode(),
	}).String()
}

func (p PortalParameters) serverName() string {
	parsed, _ := url.Parse("https://" + p.Host)
	return parsed.Hostname()
}

func (c *Client) Login(username, password string) (LoginResult, error) {
	return c.LoginContext(context.Background(), username, password)
}

func (c *Client) LoginContext(ctx context.Context, username, password string) (LoginResult, error) {
	portal, err := c.discoverPortal(ctx)
	if err != nil {
		return LoginResult{}, err
	}
	return c.loginWithPortal(ctx, portal, username, password)
}

func (c *Client) discoverPortal(ctx context.Context) (PortalParameters, error) {
	for _, probeURL := range c.probeURLs {
		portal, err := c.probePortal(ctx, probeURL)
		if err == nil {
			return portal, nil
		}
	}
	if c.fallback != nil {
		return *c.fallback, nil
	}
	return PortalParameters{}, errors.New("portal discovery failed: no valid captive-portal redirect was received; no static endpoint fallback is configured")
}

func (c *Client) probePortal(ctx context.Context, probeURL string) (PortalParameters, error) {
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, probeURL, nil)
	if err != nil {
		return PortalParameters{}, errors.New("invalid portal discovery probe URL")
	}
	response, err := c.discoveryClient.Do(request)
	if err != nil {
		return PortalParameters{}, errors.New("portal discovery probe did not complete")
	}
	defer response.Body.Close()

	if response.StatusCode < http.StatusMultipleChoices || response.StatusCode >= http.StatusBadRequest {
		return PortalParameters{}, errors.New("portal discovery probe was not redirected")
	}
	location := response.Header.Get("Location")
	if location == "" {
		return PortalParameters{}, errors.New("portal discovery redirect did not contain a Location header")
	}
	return ParsePortalRedirect(location)
}

func (c *Client) loginWithPortal(ctx context.Context, portal PortalParameters, username, password string) (LoginResult, error) {
	return loginWithHTTPClient(ctx, newPortalHTTPClient(portal), portal, username, password)
}

// loginWithHTTPClient contains the SRun protocol flow separately from socket
// setup so the complete discovered-parameter flow can be tested locally.
func loginWithHTTPClient(ctx context.Context, httpClient *http.Client, portal PortalParameters, username, password string) (LoginResult, error) {

	challenge, err := getChallenge(ctx, httpClient, portal, username)
	if err != nil {
		return LoginResult{}, fmt.Errorf("get login challenge: %w", err)
	}

	encryptedPassword := encryptPassword(challenge, password)
	info := userInfo{Username: username, Password: password, IP: portal.ClientIP, Acid: portal.ACID, EncVer: "srun_bx1"}
	encodedInfo, err := info.encode(challenge)
	if err != nil {
		return LoginResult{}, fmt.Errorf("encode login data: %w", err)
	}

	checksum := sha1Hex(challenge + username + challenge + encryptedPassword + challenge + portal.ACID + challenge + portal.ClientIP + challenge + "200" + challenge + "1" + challenge + encodedInfo)
	parameters := url.Values{}
	parameters.Set("action", "login")
	parameters.Set("ac_id", portal.ACID)
	parameters.Set("n", "200")
	parameters.Set("type", "1")
	parameters.Set("ip", portal.ClientIP)
	parameters.Set("username", username)
	parameters.Set("password", "{MD5}"+encryptedPassword)
	parameters.Set("info", encodedInfo)
	parameters.Set("chksum", checksum)
	parameters.Set("callback", callback)

	body, err := get(ctx, httpClient, portal.endpoint("/cgi-bin/srun_portal", parameters))
	if err != nil {
		return LoginResult{}, fmt.Errorf("send login request: %w", err)
	}

	var response struct {
		Res    string `json:"res"`
		SucMsg string `json:"suc_msg"`
	}
	if err := json.Unmarshal(callbackPayload(body), &response); err != nil {
		return LoginResult{}, errors.New("portal returned an unreadable login response")
	}
	if response.Res != "ok" {
		return LoginResult{}, errors.New("portal rejected the login request")
	}
	if response.SucMsg == "ip_already_online_error" {
		return LoginResult{Message: "Already online."}, nil
	}
	return LoginResult{Message: "Campus login completed."}, nil
}

func newPortalHTTPClient(portal PortalParameters) *http.Client {
	dialer := &net.Dialer{Timeout: 10 * time.Second}
	return &http.Client{
		Transport: &http.Transport{
			Proxy: nil,
			// The request URL still contains portal.Host. This preserves both the
			// Host header and TLS SNI/certificate validation while TCP connects to
			// the AC address learned from the current redirect.
			TLSClientConfig:       &tls.Config{ServerName: portal.serverName(), MinVersion: tls.VersionTLS12},
			TLSHandshakeTimeout:   10 * time.Second,
			ResponseHeaderTimeout: 10 * time.Second,
			DialContext: func(ctx context.Context, network, address string) (net.Conn, error) {
				host, port, err := net.SplitHostPort(address)
				if err != nil || !strings.EqualFold(host, portal.serverName()) {
					return nil, errors.New("portal request did not target the discovered portal host")
				}
				return dialer.DialContext(ctx, network, net.JoinHostPort(portal.ACIP, port))
			},
		},
		Timeout: 20 * time.Second,
	}
}

func getChallenge(ctx context.Context, httpClient *http.Client, portal PortalParameters, username string) (string, error) {
	parameters := url.Values{}
	parameters.Set("username", username)
	parameters.Set("ip", portal.ClientIP)
	parameters.Set("ac_id", portal.ACID)
	parameters.Set("callback", callback)
	body, err := get(ctx, httpClient, portal.endpoint("/cgi-bin/get_challenge", parameters))
	if err != nil {
		return "", err
	}
	var response struct {
		Challenge string `json:"challenge"`
		Error     string `json:"error"`
	}
	if err := json.Unmarshal(callbackPayload(body), &response); err != nil {
		return "", errors.New("portal returned an unreadable challenge response")
	}
	if response.Error != "ok" {
		return "", errors.New("portal rejected the challenge request")
	}
	if response.Challenge == "" {
		return "", errors.New("portal returned an empty challenge")
	}
	return response.Challenge, nil
}

func get(ctx context.Context, httpClient *http.Client, requestURL string) ([]byte, error) {
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, requestURL, nil)
	if err != nil {
		return nil, errors.New("could not build a portal request")
	}
	response, err := httpClient.Do(request)
	if err != nil {
		if errors.Is(err, context.DeadlineExceeded) {
			return nil, errors.New("direct request to the discovered portal timed out; no static endpoint fallback was used")
		}
		return nil, errors.New("direct request to the discovered portal failed; no static endpoint fallback was used")
	}
	defer response.Body.Close()
	if response.StatusCode < http.StatusOK || response.StatusCode >= http.StatusMultipleChoices {
		return nil, fmt.Errorf("portal returned HTTP status %s", response.Status)
	}
	return io.ReadAll(response.Body)
}

func callbackPayload(body []byte) []byte {
	trimmed := strings.TrimSpace(string(body))
	prefix := callback + "("
	if strings.HasPrefix(trimmed, prefix) && strings.HasSuffix(trimmed, ")") {
		return []byte(trimmed[len(prefix) : len(trimmed)-1])
	}
	return body
}

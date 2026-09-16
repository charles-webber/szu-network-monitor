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
	"regexp"
	"strings"
	"time"
)

const (
	serverHost = "net.szu.edu.cn"
	serverIP   = "198.18.6.157"
	callback   = "callback"
	acID       = "12"
)

type LoginResult struct {
	Message string
}

type Client struct {
	httpClient *http.Client
}

func NewClient() *Client {
	dialer := &net.Dialer{Timeout: 10 * time.Second}
	transport := &http.Transport{
		// Auth requests must use the campus connection directly. A local proxy
		// could otherwise resolve the hostname before this transport can map it.
		Proxy:                 nil,
		TLSClientConfig:       &tls.Config{ServerName: serverHost, MinVersion: tls.VersionTLS12},
		TLSHandshakeTimeout:   10 * time.Second,
		ResponseHeaderTimeout: 10 * time.Second,
		DialContext: func(ctx context.Context, network, address string) (net.Conn, error) {
			host, port, err := net.SplitHostPort(address)
			if err == nil && strings.EqualFold(host, serverHost) {
				address = net.JoinHostPort(serverIP, port)
			}
			return dialer.DialContext(ctx, network, address)
		},
	}
	return &Client{httpClient: &http.Client{Transport: transport, Timeout: 20 * time.Second}}
}

func (c *Client) Login(username, password string) (LoginResult, error) {
	ip, err := c.getIP()
	if err != nil {
		return LoginResult{}, fmt.Errorf("get campus IP: %w", err)
	}

	challenge, err := c.getChallenge(username, ip)
	if err != nil {
		return LoginResult{}, fmt.Errorf("get login challenge: %w", err)
	}

	encryptedPassword := encryptPassword(challenge, password)
	info := userInfo{Username: username, Password: password, IP: ip, Acid: acID, EncVer: "srun_bx1"}
	encodedInfo, err := info.encode(challenge)
	if err != nil {
		return LoginResult{}, fmt.Errorf("encode login data: %w", err)
	}

	checksum := sha1Hex(challenge + username + challenge + encryptedPassword + challenge + acID + challenge + ip + challenge + "200" + challenge + "1" + challenge + encodedInfo)
	parameters := url.Values{}
	parameters.Set("action", "login")
	parameters.Set("ac_id", acID)
	parameters.Set("n", "200")
	parameters.Set("type", "1")
	parameters.Set("ip", ip)
	parameters.Set("username", username)
	parameters.Set("password", "{MD5}"+encryptedPassword)
	parameters.Set("info", encodedInfo)
	parameters.Set("chksum", checksum)
	parameters.Set("callback", callback)

	body, err := c.get("https://" + serverHost + "/cgi-bin/srun_portal?" + parameters.Encode())
	if err != nil {
		return LoginResult{}, fmt.Errorf("send login request: %w", err)
	}

	var response struct {
		Res      string `json:"res"`
		SucMsg   string `json:"suc_msg"`
		ErrorMsg string `json:"error_msg"`
	}
	if err := json.Unmarshal(callbackPayload(body), &response); err != nil {
		return LoginResult{}, fmt.Errorf("parse login response: %w", err)
	}
	if response.Res != "ok" {
		if response.ErrorMsg == "" {
			response.ErrorMsg = "the portal returned an unsuccessful login response"
		}
		return LoginResult{}, errors.New(response.ErrorMsg)
	}
	if response.SucMsg == "ip_already_online_error" {
		return LoginResult{Message: "Already online."}, nil
	}
	if response.SucMsg == "" {
		return LoginResult{Message: "Campus login completed."}, nil
	}
	return LoginResult{Message: response.SucMsg}, nil
}

func (c *Client) getIP() (string, error) {
	body, err := c.get("https://" + serverHost + "/srun_portal_success?ac_id=" + acID + "&theme=proyx")
	if err != nil {
		return "", err
	}
	matches := regexp.MustCompile(`ip\s*:\s*"([^"]+)"`).FindStringSubmatch(string(body))
	if len(matches) < 2 {
		return "", errors.New("campus IP was not present in the portal response")
	}
	return matches[1], nil
}

func (c *Client) getChallenge(username, ip string) (string, error) {
	parameters := url.Values{}
	parameters.Set("username", username)
	parameters.Set("ip", ip)
	parameters.Set("callback", callback)
	body, err := c.get("https://" + serverHost + "/cgi-bin/get_challenge?" + parameters.Encode())
	if err != nil {
		return "", err
	}
	var response struct {
		Challenge string `json:"challenge"`
		Error     string `json:"error"`
	}
	if err := json.Unmarshal(callbackPayload(body), &response); err != nil {
		return "", err
	}
	if response.Error != "ok" {
		return "", errors.New(response.Error)
	}
	if response.Challenge == "" {
		return "", errors.New("portal returned an empty challenge")
	}
	return response.Challenge, nil
}

func (c *Client) get(requestURL string) ([]byte, error) {
	response, err := c.httpClient.Get(requestURL)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()
	if response.StatusCode < http.StatusOK || response.StatusCode >= http.StatusMultipleChoices {
		return nil, fmt.Errorf("unexpected HTTP status %s", response.Status)
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

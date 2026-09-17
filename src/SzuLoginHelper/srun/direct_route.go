package srun

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"regexp"
	"strings"
	"time"
)

const directPortalHost = "net.szu.edu.cn"

var (
	acIDPattern = regexp.MustCompile(`(?i)(?:[?&]ac_id=|["']ac_id["']\s*[:=]\s*["']?)(\d+)`)
	acIPPattern = regexp.MustCompile(`(?i)(?:[?&]ac[-_]ip=|["']ac[-_]ip["']\s*[:=]\s*["']?)(\d{1,3}(?:\.\d{1,3}){3})`)
)

// directRoute is provided by the Windows GUI after it identifies the active
// physical campus adapter. Its DNS queries and TCP connections are bound to
// that adapter, avoiding proxy/TUN routing and Fake-IP answers.
type directRoute struct {
	sourceIP   net.IP
	dnsServers []net.IP
}

func directRouteFromEnvironment() (*directRoute, error) {
	sourceText := strings.TrimSpace(os.Getenv("SZU_DIRECT_SOURCE_IP"))
	dnsText := strings.TrimSpace(os.Getenv("SZU_DIRECT_DNS_SERVERS"))
	if sourceText == "" && dnsText == "" {
		return nil, nil
	}

	sourceIP := net.ParseIP(sourceText).To4()
	if sourceIP == nil || !isUsableDirectIPv4(sourceIP) {
		return nil, errors.New("physical campus adapter did not provide a usable IPv4 address")
	}

	var dnsServers []net.IP
	for _, value := range strings.Split(dnsText, ",") {
		address := net.ParseIP(strings.TrimSpace(value)).To4()
		if address != nil && isUsableDirectIPv4(address) {
			dnsServers = append(dnsServers, address)
		}
	}
	if len(dnsServers) == 0 {
		return nil, errors.New("physical campus adapter did not provide a usable DNS server")
	}

	return &directRoute{sourceIP: sourceIP, dnsServers: dnsServers}, nil
}

func isUsableDirectIPv4(address net.IP) bool {
	address = address.To4()
	if address == nil || address.IsLoopback() || address.IsUnspecified() || address.IsLinkLocalUnicast() {
		return false
	}
	return address[0] != 198 || (address[1] != 18 && address[1] != 19)
}

func (route *directRoute) resolvePortal(ctx context.Context) (net.IP, error) {
	resolver := &net.Resolver{
		PreferGo: true,
		Dial: func(ctx context.Context, network, _ string) (net.Conn, error) {
			var lastError error
			for _, dnsServer := range route.dnsServers {
				dialer := &net.Dialer{Timeout: 3 * time.Second}
				if network == "tcp" {
					dialer.LocalAddr = &net.TCPAddr{IP: route.sourceIP}
				} else {
					dialer.LocalAddr = &net.UDPAddr{IP: route.sourceIP}
				}
				connection, err := dialer.DialContext(ctx, network, net.JoinHostPort(dnsServer.String(), "53"))
				if err == nil {
					return connection, nil
				}
				lastError = err
			}
			if lastError == nil {
				lastError = errors.New("no DNS server was available")
			}
			return nil, lastError
		},
	}

	lookupContext, cancel := context.WithTimeout(ctx, 8*time.Second)
	defer cancel()
	addresses, err := resolver.LookupIPAddr(lookupContext, directPortalHost)
	if err != nil {
		return nil, errors.New("could not resolve the SRun portal through the physical campus adapter")
	}
	for _, address := range addresses {
		if ipv4 := address.IP.To4(); isUsableDirectIPv4(ipv4) {
			return ipv4, nil
		}
	}
	return nil, errors.New("physical campus DNS returned no usable SRun portal address")
}

func (route *directRoute) discoverPortal(ctx context.Context) (PortalParameters, error) {
	portalIP, err := route.resolvePortal(ctx)
	if err != nil {
		return PortalParameters{}, err
	}

	portal := PortalParameters{Host: directPortalHost, ACIP: portalIP.String()}
	httpClient := newPortalHTTPClientForSource(portal, route.sourceIP)
	for _, path := range []string{"/srun_portal_pc", "/", "/index_1.html"} {
		request, err := http.NewRequestWithContext(ctx, http.MethodGet, portal.endpoint(path, nil), nil)
		if err != nil {
			continue
		}
		response, err := httpClient.Do(request)
		if err != nil {
			continue
		}
		body, _ := io.ReadAll(io.LimitReader(response.Body, 512*1024))
		response.Body.Close()

		if discovered, ok := portalFromDirectResponse(portal, response.Header.Get("Location"), string(body)); ok {
			portal = discovered
			return portal, nil
		}
	}

	return PortalParameters{}, errors.New("SRun portal did not provide a dynamic ac_id for the current network")
}

func acIDFromPortalResponse(values ...string) string {
	for _, value := range values {
		if match := acIDPattern.FindStringSubmatch(value); len(match) == 2 {
			return match[1]
		}
	}
	return ""
}

// portalFromDirectResponse preserves net.szu.edu.cn as the HTTPS authority
// while preferring the AC address embedded in the portal response as the TCP
// destination. The challenge response remains the final authority for the
// client's current address.
func portalFromDirectResponse(portal PortalParameters, values ...string) (PortalParameters, bool) {
	acID := acIDFromPortalResponse(values...)
	if acID == "" {
		return PortalParameters{}, false
	}
	portal.ACID = acID

	for _, value := range values {
		match := acIPPattern.FindStringSubmatch(value)
		if len(match) != 2 {
			continue
		}
		if address := net.ParseIP(match[1]).To4(); isUsableDirectIPv4(address) {
			portal.ACIP = address.String()
			break
		}
	}

	return portal, true
}

func (route *directRoute) diagnostic(ctx context.Context) error {
	portal, err := route.discoverPortal(ctx)
	if err != nil {
		return err
	}
	if portal.ACID == "" {
		return fmt.Errorf("SRun portal did not provide a dynamic ac_id")
	}
	return nil
}

// DiagnoseDirect verifies that the physical-adapter route can reach the SRun
// portal and obtain a dynamic ac_id without sending any credentials.
func (c *Client) DiagnoseDirect(ctx context.Context) error {
	if c.directRoute == nil {
		if c.directRouteErr != nil {
			return c.directRouteErr
		}
		return errors.New("physical campus adapter context was not provided")
	}
	return c.directRoute.diagnostic(ctx)
}

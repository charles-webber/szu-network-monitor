package srun

import (
	"context"
	"io"
	"net/http"
	"strings"
	"testing"
)

func TestCustomBase64UsesExpectedAlphabet(t *testing.T) {
	if got, want := customBase64([]byte{0, 0, 0}), "LLLL"; got != want {
		t.Fatalf("customBase64 returned %q, want %q", got, want)
	}
	if got, want := customBase64([]byte{255}), "Av=="; got != want {
		t.Fatalf("customBase64 returned %q, want %q", got, want)
	}
}

func TestCallbackPayload(t *testing.T) {
	if got, want := string(callbackPayload([]byte("callback({\"error\":\"ok\"})"))), "{\"error\":\"ok\"}"; got != want {
		t.Fatalf("callbackPayload returned %q, want %q", got, want)
	}
}

func TestParsePortalRedirectAcceptsHyphenatedACIP(t *testing.T) {
	portal, err := ParsePortalRedirect("https://net.szu.edu.cn/srun_portal_success?ac-ip=172.31.254.29&ac_id=2&ssid=SZU_WLAN&uaddress=10.20.30.40")
	if err != nil {
		t.Fatalf("ParsePortalRedirect returned error: %v", err)
	}
	if got, want := portal.Host, "net.szu.edu.cn"; got != want {
		t.Errorf("Host = %q, want %q", got, want)
	}
	if got, want := portal.ACID, "2"; got != want {
		t.Errorf("ACID = %q, want %q", got, want)
	}
	if got, want := portal.ACIP, "172.31.254.29"; got != want {
		t.Errorf("ACIP = %q, want %q", got, want)
	}
	if got, want := portal.SSID, "SZU_WLAN"; got != want {
		t.Errorf("SSID = %q, want %q", got, want)
	}
	if got, want := portal.ClientIP, "10.20.30.40"; got != want {
		t.Errorf("ClientIP = %q, want %q", got, want)
	}
}

func TestParsePortalRedirectAcceptsUnderscoredACIPAndDifferentACID(t *testing.T) {
	portal, err := ParsePortalRedirect("https://net.szu.edu.cn/srun_portal_success?ac_ip=172.16.8.9&ac_id=7&ssid=Teaching%20Area&user_ip=192.168.5.6")
	if err != nil {
		t.Fatalf("ParsePortalRedirect returned error: %v", err)
	}
	if got, want := portal.ACID, "7"; got != want {
		t.Errorf("ACID = %q, want %q", got, want)
	}
	if got, want := portal.ACIP, "172.16.8.9"; got != want {
		t.Errorf("ACIP = %q, want %q", got, want)
	}
	if got, want := portal.SSID, "Teaching Area"; got != want {
		t.Errorf("SSID = %q, want %q", got, want)
	}
	if got, want := portal.ClientIP, "192.168.5.6"; got != want {
		t.Errorf("ClientIP = %q, want %q", got, want)
	}
}

func TestGetChallengeUsesDiscoveredACID(t *testing.T) {
	portal := PortalParameters{Host: "net.szu.edu.cn", ACID: "7", ACIP: "172.16.8.9", ClientIP: "192.168.5.6"}
	client := &http.Client{Transport: roundTripper(func(request *http.Request) (*http.Response, error) {
		if got, want := request.URL.Query().Get("ac_id"), "7"; got != want {
			t.Errorf("challenge ac_id = %q, want %q", got, want)
		}
		return &http.Response{
			StatusCode: http.StatusOK,
			Status:     "200 OK",
			Header:     make(http.Header),
			Body:       io.NopCloser(strings.NewReader(`callback({"error":"ok","challenge":"token"})`)),
		}, nil
	})}

	challenge, err := getChallenge(context.Background(), client, portal, "account")
	if err != nil {
		t.Fatalf("getChallenge returned error: %v", err)
	}
	if got, want := challenge, "token"; got != want {
		t.Errorf("challenge = %q, want %q", got, want)
	}
}

type roundTripper func(*http.Request) (*http.Response, error)

func (f roundTripper) RoundTrip(request *http.Request) (*http.Response, error) {
	return f(request)
}

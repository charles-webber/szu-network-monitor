package srun

import (
	"context"
	"io"
	"net/http"
	"net/http/httptest"
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

func TestProbePortalReadsRedirectWithoutFollowingIt(t *testing.T) {
	redirectWasFollowed := false
	server := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
		if request.URL.Path != "/probe" {
			redirectWasFollowed = true
		}
		response.Header().Set("Location", "http://"+request.Host+"/portal?ac_ip=172.16.8.9&ac_id=7&ssid=SZU_WLAN&uaddress=10.20.30.40")
		response.WriteHeader(http.StatusFound)
	}))
	defer server.Close()

	client := NewClient(ClientOptions{ProbeURLs: []string{server.URL + "/probe"}})
	portal, err := client.probePortal(context.Background(), server.URL+"/probe")
	if err != nil {
		t.Fatalf("probePortal returned error: %v", err)
	}
	if redirectWasFollowed {
		t.Fatal("portal discovery followed the captive-portal redirect")
	}
	if got, want := portal.ACID, "7"; got != want {
		t.Errorf("ACID = %q, want %q", got, want)
	}
	if got, want := portal.ACIP, "172.16.8.9"; got != want {
		t.Errorf("ACIP = %q, want %q", got, want)
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
			Body:       io.NopCloser(strings.NewReader(`callback({"error":"ok","challenge":"token","client_ip":"192.168.5.6"})`)),
		}, nil
	})}

	challenge, err := getChallenge(context.Background(), client, portal, "account")
	if err != nil {
		t.Fatalf("getChallenge returned error: %v", err)
	}
	if got, want := challenge.Challenge, "token"; got != want {
		t.Errorf("challenge = %q, want %q", got, want)
	}
	if got, want := challenge.ClientIP, "192.168.5.6"; got != want {
		t.Errorf("challenge client IP = %q, want %q", got, want)
	}
}

func TestLoginFlowUsesEveryDiscoveredParameter(t *testing.T) {
	portal := PortalParameters{Host: "net.szu.edu.cn", ACID: "9", ACIP: "172.16.8.9", SSID: "SZU_WLAN", ClientIP: "192.168.5.6"}
	const username = "account"
	const password = "password"
	const challenge = "token"

	client := &http.Client{Transport: roundTripper(func(request *http.Request) (*http.Response, error) {
		query := request.URL.Query()
		switch request.URL.Path {
		case "/cgi-bin/get_challenge":
			if got, want := query.Get("ac_id"), portal.ACID; got != want {
				t.Errorf("challenge ac_id = %q, want %q", got, want)
			}
			if got, want := query.Get("ip"), portal.ClientIP; got != want {
				t.Errorf("challenge IP = %q, want %q", got, want)
			}
			return callbackResponse(`callback({"error":"ok","challenge":"token"})`), nil
		case "/cgi-bin/srun_portal":
			if got, want := query.Get("ac_id"), portal.ACID; got != want {
				t.Errorf("login ac_id = %q, want %q", got, want)
			}
			if got, want := query.Get("ip"), portal.ClientIP; got != want {
				t.Errorf("login IP = %q, want %q", got, want)
			}
			if got, want := query.Get("password"), "{MD5}"+encryptPassword(challenge, password); got != want {
				t.Errorf("login password digest = %q, want %q", got, want)
			}
			info := userInfo{Username: username, Password: password, IP: portal.ClientIP, Acid: portal.ACID, EncVer: "srun_bx1"}
			expectedInfo, err := info.encode(challenge)
			if err != nil {
				t.Fatalf("info.encode returned error: %v", err)
			}
			if got := query.Get("info"); got != expectedInfo {
				t.Errorf("login info did not include the discovered ac_id and IP")
			}
			expectedChecksum := sha1Hex(challenge + username + challenge + encryptPassword(challenge, password) + challenge + portal.ACID + challenge + portal.ClientIP + challenge + "200" + challenge + "1" + challenge + expectedInfo)
			if got, want := query.Get("chksum"), expectedChecksum; got != want {
				t.Errorf("login checksum = %q, want %q", got, want)
			}
			return callbackResponse(`callback({"res":"ok","suc_msg":"login_ok"})`), nil
		default:
			t.Fatalf("unexpected portal request: %s", request.URL)
			return nil, nil
		}
	})}

	result, err := loginWithHTTPClient(context.Background(), client, portal, username, password)
	if err != nil {
		t.Fatalf("loginWithHTTPClient returned error: %v", err)
	}
	if got, want := result.Message, "Campus login completed."; got != want {
		t.Errorf("message = %q, want %q", got, want)
	}
}

func callbackResponse(body string) *http.Response {
	return &http.Response{
		StatusCode: http.StatusOK,
		Status:     "200 OK",
		Header:     make(http.Header),
		Body:       io.NopCloser(strings.NewReader(body)),
	}
}

type roundTripper func(*http.Request) (*http.Response, error)

func (f roundTripper) RoundTrip(request *http.Request) (*http.Response, error) {
	return f(request)
}

package srun

import (
	"net"
	"testing"
)

func TestUsableDirectIPv4RejectsFakeAndNonRoutableAddresses(t *testing.T) {
	testCases := []struct {
		address string
		valid   bool
	}{
		{"172.28.192.101", true},
		{"10.0.0.1", true},
		{"198.18.6.157", false},
		{"198.19.1.1", false},
		{"169.254.1.1", false},
		{"127.0.0.1", false},
	}

	for _, testCase := range testCases {
		t.Run(testCase.address, func(t *testing.T) {
			if got := isUsableDirectIPv4(net.ParseIP(testCase.address)); got != testCase.valid {
				t.Errorf("isUsableDirectIPv4(%s) = %t, want %t", testCase.address, got, testCase.valid)
			}
		})
	}
}

func TestACIDFromPortalResponse(t *testing.T) {
	if got, want := acIDFromPortalResponse(`<a href="/srun_portal_pc?ac_id=2">login</a>`), "2"; got != want {
		t.Errorf("acIDFromPortalResponse = %q, want %q", got, want)
	}
	if got, want := acIDFromPortalResponse(`window.config = {"ac_id":"7"}`), "7"; got != want {
		t.Errorf("acIDFromPortalResponse = %q, want %q", got, want)
	}
	if got := acIDFromPortalResponse(`no dynamic parameter`); got != "" {
		t.Errorf("acIDFromPortalResponse = %q, want empty", got)
	}
}

func TestPortalFromDirectResponsePrefersDiscoveredACIP(t *testing.T) {
	testCases := []struct {
		name     string
		response string
		acID     string
		acIP     string
	}{
		{"hyphenated AC address", `https://net.szu.edu.cn/srun_portal_pc?ac-ip=172.31.254.29&ac_id=2`, "2", "172.31.254.29"},
		{"underscored AC address", `window.config = {"ac_ip":"172.16.8.9", "ac_id":"7"}`, "7", "172.16.8.9"},
	}

	for _, testCase := range testCases {
		t.Run(testCase.name, func(t *testing.T) {
			portal, ok := portalFromDirectResponse(
				PortalParameters{Host: directPortalHost, ACIP: "172.31.254.1"},
				testCase.response,
			)
			if !ok {
				t.Fatal("portalFromDirectResponse did not find ac_id")
			}
			if got, want := portal.ACID, testCase.acID; got != want {
				t.Errorf("ACID = %q, want %q", got, want)
			}
			if got, want := portal.ACIP, testCase.acIP; got != want {
				t.Errorf("ACIP = %q, want %q", got, want)
			}
		})
	}
}

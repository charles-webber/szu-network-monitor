package srun

import "testing"

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

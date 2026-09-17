package main

import (
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"strings"
	"time"

	"github.com/hyyyyyy/szu-network-monitor/login-helper/srun"
)

type output struct {
	Success bool   `json:"success"`
	Message string `json:"message"`
	Error   string `json:"error,omitempty"`
}

func main() {
	username := flag.String("username", "", "campus network username")
	passwordFromStdin := flag.Bool("password-stdin", false, "read password from standard input")
	jsonOutput := flag.Bool("json", false, "write a JSON result to standard output")
	diagnoseDirect := flag.Bool("diagnose-direct", false, "verify direct SRun discovery without submitting credentials")
	probeURL := flag.String("probe-url", "", "optional HTTP captive-portal probe URL")
	fallbackHost := flag.String("fallback-host", "", "explicit fallback portal host (used only if discovery fails)")
	fallbackACID := flag.String("fallback-ac-id", "", "explicit fallback ac_id (used only if discovery fails)")
	fallbackACIP := flag.String("fallback-ac-ip", "", "explicit fallback AC IP (used only if discovery fails)")
	fallbackClientIP := flag.String("fallback-client-ip", "", "explicit fallback client IP (used only if discovery fails)")
	flag.Parse()

	options := srun.ClientOptions{}
	if strings.TrimSpace(*probeURL) != "" {
		options.ProbeURLs = []string{strings.TrimSpace(*probeURL)}
	}
	fallbackValues := []string{*fallbackHost, *fallbackACID, *fallbackACIP, *fallbackClientIP}
	hasFallbackValue := false
	for _, value := range fallbackValues {
		hasFallbackValue = hasFallbackValue || strings.TrimSpace(value) != ""
	}
	if hasFallbackValue {
		fallback, fallbackErr := srun.NewFallbackPortal(*fallbackHost, *fallbackACID, *fallbackACIP, *fallbackClientIP)
		if fallbackErr != nil {
			writeResult(*jsonOutput, output{Success: false, Message: "Invalid fallback portal configuration.", Error: "All fallback portal values must be valid and explicitly supplied."})
			os.Exit(2)
		}
		options.Fallback = &fallback
	}

	client := srun.NewClient(options)
	if *diagnoseDirect {
		diagnosticsContext, cancel := context.WithTimeout(context.Background(), 20*time.Second)
		defer cancel()
		if err := client.DiagnoseDirect(diagnosticsContext); err != nil {
			writeResult(*jsonOutput, output{Success: false, Message: "Direct SRun discovery failed.", Error: err.Error()})
			os.Exit(1)
		}
		writeResult(*jsonOutput, output{Success: true, Message: "Direct SRun portal discovery succeeded."})
		return
	}

	if strings.TrimSpace(*username) == "" || !*passwordFromStdin {
		writeResult(*jsonOutput, output{Success: false, Message: "Missing username or password input.", Error: "Use --username and --password-stdin."})
		os.Exit(2)
	}

	passwordBytes, err := io.ReadAll(os.Stdin)
	if err != nil {
		writeResult(*jsonOutput, output{Success: false, Message: "Unable to read password input.", Error: "Unable to read password input."})
		os.Exit(2)
	}
	password := strings.TrimSuffix(strings.TrimSuffix(string(passwordBytes), "\n"), "\r")
	if password == "" {
		writeResult(*jsonOutput, output{Success: false, Message: "Password is empty.", Error: "No password was received on standard input."})
		os.Exit(2)
	}

	result, err := client.Login(strings.TrimSpace(*username), password)
	if err != nil {
		writeResult(*jsonOutput, output{Success: false, Message: "Campus login failed.", Error: err.Error()})
		os.Exit(1)
	}

	writeResult(*jsonOutput, output{Success: true, Message: result.Message})
}

func writeResult(asJSON bool, result output) {
	if asJSON {
		encoded, _ := json.Marshal(result)
		fmt.Println(string(encoded))
		return
	}
	if result.Success {
		fmt.Println(result.Message)
	} else {
		fmt.Fprintln(os.Stderr, result.Error)
	}
}

package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"strings"

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
	flag.Parse()

	if strings.TrimSpace(*username) == "" || !*passwordFromStdin {
		writeResult(*jsonOutput, output{Success: false, Message: "Missing username or password input.", Error: "Use --username and --password-stdin."})
		os.Exit(2)
	}

	passwordBytes, err := io.ReadAll(os.Stdin)
	if err != nil {
		writeResult(*jsonOutput, output{Success: false, Message: "Unable to read password input.", Error: err.Error()})
		os.Exit(2)
	}
	password := strings.TrimSuffix(strings.TrimSuffix(string(passwordBytes), "\n"), "\r")
	if password == "" {
		writeResult(*jsonOutput, output{Success: false, Message: "Password is empty.", Error: "No password was received on standard input."})
		os.Exit(2)
	}

	result, err := srun.NewClient().Login(strings.TrimSpace(*username), password)
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

// parent_control.go
package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"
)

const (
	reset  = "\033[0m"
	green  = "\033[92m"
	red    = "\033[91m"
	yellow = "\033[93m"
	blue   = "\033[94m"
)

func colorize(text, color string) string {
	return color + text + reset
}

type Config struct {
	Rules struct {
		Sites     []string `json:"sites"`
		TimeLimit int      `json:"time_limit"`
		Apps      []string `json:"apps"`
	} `json:"rules"`
	Active    bool   `json:"active"`
	StartTime string `json:"start_time"`
	TotalTime int    `json:"total_time"`
}

var configDir = filepath.Join(os.Getenv("HOME"), ".parent_control")
var configFile = filepath.Join(configDir, "config.json")
var hostsFile = "/etc/hosts"
var blockIP = "127.0.0.1"

func init() {
	if runtime.GOOS == "windows" {
		hostsFile = `C:\Windows\System32\drivers\etc\hosts`
	}
}

func ensureConfig() {
	os.MkdirAll(configDir, 0755)
	if _, err := os.Stat(configFile); os.IsNotExist(err) {
		cfg := Config{}
		cfg.Rules.Sites = []string{}
		cfg.Rules.Apps = []string{}
		cfg.Active = false
		cfg.TotalTime = 0
		data, _ := json.MarshalIndent(cfg, "", "  ")
		ioutil.WriteFile(configFile, data, 0644)
	}
}

func loadConfig() Config {
	ensureConfig()
	data, _ := ioutil.ReadFile(configFile)
	var cfg Config
	json.Unmarshal(data, &cfg)
	return cfg
}

func saveConfig(cfg Config) {
	data, _ := json.MarshalIndent(cfg, "", "  ")
	ioutil.WriteFile(configFile, data, 0644)
}

func readHosts() []string {
	f, _ := os.Open(hostsFile)
	defer f.Close()
	var lines []string
	scanner := bufio.NewScanner(f)
	for scanner.Scan() {
		lines = append(lines, scanner.Text())
	}
	return lines
}

func writeHosts(lines []string) {
	ioutil.WriteFile(hostsFile, []byte(strings.Join(lines, "\n")), 0644)
}

func updateHosts(domains []string, action string) {
	lines := readHosts()
	var newLines []string
	for _, line := range lines {
		skip := false
		for _, d := range domains {
			if strings.TrimSpace(line)[:len(blockIP)] == blockIP && strings.Contains(line, d) {
				skip = true
				break
			}
		}
		if !skip {
			newLines = append(newLines, line)
		}
	}
	if action == "add" {
		for _, d := range domains {
			newLines = append(newLines, fmt.Sprintf("%s %s", blockIP, d))
		}
	}
	writeHosts(newLines)
}

func addRule(ruleType, value string) {
	cfg := loadConfig()
	switch ruleType {
	case "site":
		for _, s := range cfg.Rules.Sites {
			if s == value {
				fmt.Println(colorize("Site already in list", yellow))
				return
			}
		}
		cfg.Rules.Sites = append(cfg.Rules.Sites, value)
		updateHosts([]string{value}, "add")
	case "time":
		minutes, err := strconv.Atoi(value)
		if err != nil {
			fmt.Println(colorize("Invalid time value", red))
			return
		}
		cfg.Rules.TimeLimit = minutes
	case "app":
		for _, a := range cfg.Rules.Apps {
			if a == value {
				fmt.Println(colorize("App already in list", yellow))
				return
			}
		}
		cfg.Rules.Apps = append(cfg.Rules.Apps, value)
	default:
		fmt.Println(colorize("Unknown rule type", red))
		return
	}
	saveConfig(cfg)
	fmt.Println(colorize("Rule added: "+ruleType+" -> "+value, green))
}

func removeRule(ruleType, value string) {
	cfg := loadConfig()
	switch ruleType {
	case "site":
		newSites := []string{}
		for _, s := range cfg.Rules.Sites {
			if s != value {
				newSites = append(newSites, s)
			}
		}
		cfg.Rules.Sites = newSites
		updateHosts([]string{value}, "remove")
	case "time":
		cfg.Rules.TimeLimit = 0
	case "app":
		newApps := []string{}
		for _, a := range cfg.Rules.Apps {
			if a != value {
				newApps = append(newApps, a)
			}
		}
		cfg.Rules.Apps = newApps
	default:
		fmt.Println(colorize("Unknown rule type", red))
		return
	}
	saveConfig(cfg)
	fmt.Println(colorize("Rule removed: "+ruleType+" -> "+value, green))
}

func listRules() {
	cfg := loadConfig()
	fmt.Println(colorize("Current rules:", blue))
	if len(cfg.Rules.Sites) > 0 {
		fmt.Println(colorize("  Blocked sites:", yellow))
		for _, s := range cfg.Rules.Sites {
			fmt.Printf("    - %s\n", s)
		}
	}
	if cfg.Rules.TimeLimit > 0 {
		fmt.Println(colorize(fmt.Sprintf("  Time limit: %d min", cfg.Rules.TimeLimit), yellow))
	}
	if len(cfg.Rules.Apps) > 0 {
		fmt.Println(colorize("  Blocked apps:", yellow))
		for _, a := range cfg.Rules.Apps {
			fmt.Printf("    - %s\n", a)
		}
	}
	if len(cfg.Rules.Sites) == 0 && cfg.Rules.TimeLimit == 0 && len(cfg.Rules.Apps) == 0 {
		fmt.Println(colorize("  No rules.", yellow))
	}
}

func enableControl() {
	cfg := loadConfig()
	if cfg.Active {
		fmt.Println(colorize("Parent control already enabled.", yellow))
		return
	}
	cfg.Active = true
	cfg.StartTime = time.Now().Format(time.RFC3339)
	saveConfig(cfg)
	if len(cfg.Rules.Sites) > 0 {
		updateHosts(cfg.Rules.Sites, "add")
	}
	fmt.Println(colorize("Parent control enabled.", green))
}

func disableControl() {
	cfg := loadConfig()
	if !cfg.Active {
		fmt.Println(colorize("Parent control already disabled.", yellow))
		return
	}
	cfg.Active = false
	if len(cfg.Rules.Sites) > 0 {
		updateHosts(cfg.Rules.Sites, "remove")
	}
	saveConfig(cfg)
	fmt.Println(colorize("Parent control disabled.", green))
}

func showStatus() {
	cfg := loadConfig()
	status := "DISABLED"
	if cfg.Active {
		status = "ENABLED"
	}
	fmt.Println(colorize(fmt.Sprintf("Parent control: %s", status), blue))
	if cfg.Active && cfg.StartTime != "" {
		start, _ := time.Parse(time.RFC3339, cfg.StartTime)
		elapsed := int(time.Since(start).Minutes())
		fmt.Println(colorize(fmt.Sprintf("Session time: %d min", elapsed), yellow))
		if cfg.Rules.TimeLimit > 0 {
			fmt.Println(colorize(fmt.Sprintf("Time limit: %d min", cfg.Rules.TimeLimit), yellow))
		}
	}
	fmt.Println(colorize(fmt.Sprintf("Blocked sites: %d", len(cfg.Rules.Sites)), yellow))
	fmt.Println(colorize(fmt.Sprintf("Blocked apps: %d", len(cfg.Rules.Apps)), yellow))
}

func generateReport() {
	cfg := loadConfig()
	fmt.Println(colorize("Activity report:", blue))
	fmt.Printf("  Date: %s\n", time.Now().Format("2006-01-02"))
	fmt.Printf("  Total time: %d min\n", cfg.TotalTime)
	fmt.Printf("  Time limit: %d min\n", cfg.Rules.TimeLimit)
	fmt.Printf("  Blocked sites: %d\n", len(cfg.Rules.Sites))
	fmt.Printf("  Blocked apps: %d\n", len(cfg.Rules.Apps))
}

func main() {
	if len(os.Args) < 2 {
		fmt.Println(colorize("Usage: parent_control add|remove|list|enable|disable|status|report [args...]", yellow))
		os.Exit(1)
	}
	cmd := os.Args[1]
	args := os.Args[2:]

	switch cmd {
	case "add":
		if len(args) < 2 {
			fmt.Println(colorize("Usage: add <site|time|app> <value>", yellow))
			return
		}
		addRule(args[0], args[1])
	case "remove":
		if len(args) < 2 {
			fmt.Println(colorize("Usage: remove <site|time|app> <value>", yellow))
			return
		}
		removeRule(args[0], args[1])
	case "list":
		listRules()
	case "enable":
		enableControl()
	case "disable":
		disableControl()
	case "status":
		showStatus()
	case "report":
		generateReport()
	default:
		fmt.Println(colorize("Unknown command: "+cmd, red))
	}
}

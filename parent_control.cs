// parent_control.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Runtime.InteropServices;

class ParentControl
{
    static string Colorize(string text, string color)
    {
        string col = color switch
        {
            "green" => "\x1b[92m",
            "red" => "\x1b[91m",
            "yellow" => "\x1b[93m",
            "blue" => "\x1b[94m",
            _ => "\x1b[0m"
        };
        return col + text + "\x1b[0m";
    }

    class Config
    {
        public Rules Rules { get; set; } = new();
        public bool Active { get; set; }
        public string StartTime { get; set; }
        public int TotalTime { get; set; }
    }

    class Rules
    {
        public List<string> Sites { get; set; } = new();
        public int TimeLimit { get; set; }
        public List<string> Apps { get; set; } = new();
    }

    static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".parent_control");
    static string ConfigFile => Path.Combine(ConfigDir, "config.json");
    static string HostsFile => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"C:\Windows\System32\drivers\etc\hosts"
        : "/etc/hosts";
    const string BLOCK_IP = "127.0.0.1";

    static void EnsureConfig()
    {
        Directory.CreateDirectory(ConfigDir);
        if (!File.Exists(ConfigFile))
        {
            var def = new Config();
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    static Config LoadConfig()
    {
        EnsureConfig();
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile)) ?? new Config();
    }

    static void SaveConfig(Config cfg)
    {
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    }

    static List<string> ReadHosts()
    {
        return File.ReadAllLines(HostsFile).ToList();
    }

    static void WriteHosts(List<string> lines)
    {
        File.WriteAllLines(HostsFile, lines);
    }

    static void UpdateHosts(List<string> domains, string action)
    {
        var lines = ReadHosts();
        var newLines = lines.Where(line => !domains.Any(d => line.Trim().StartsWith(BLOCK_IP) && line.Contains(d))).ToList();
        if (action == "add")
        {
            foreach (var d in domains) newLines.Add($"{BLOCK_IP} {d}");
        }
        WriteHosts(newLines);
    }

    static void AddRule(string type, string value)
    {
        var cfg = LoadConfig();
        if (type == "site")
        {
            if (!cfg.Rules.Sites.Contains(value))
            {
                cfg.Rules.Sites.Add(value);
                UpdateHosts(new List<string>{ value }, "add");
            }
            else
            {
                Console.WriteLine(Colorize("Site already in list", "yellow"));
                return;
            }
        }
        else if (type == "time")
        {
            if (!int.TryParse(value, out int mins))
            {
                Console.WriteLine(Colorize("Invalid time value", "red"));
                return;
            }
            cfg.Rules.TimeLimit = mins;
        }
        else if (type == "app")
        {
            if (!cfg.Rules.Apps.Contains(value))
                cfg.Rules.Apps.Add(value);
            else
            {
                Console.WriteLine(Colorize("App already in list", "yellow"));
                return;
            }
        }
        else
        {
            Console.WriteLine(Colorize("Unknown rule type", "red"));
            return;
        }
        SaveConfig(cfg);
        Console.WriteLine(Colorize($"Rule added: {type} -> {value}", "green"));
    }

    static void RemoveRule(string type, string value)
    {
        var cfg = LoadConfig();
        if (type == "site")
        {
            if (cfg.Rules.Sites.Remove(value))
                UpdateHosts(new List<string>{ value }, "remove");
            else
            {
                Console.WriteLine(Colorize("Site not found", "yellow"));
                return;
            }
        }
        else if (type == "time")
        {
            cfg.Rules.TimeLimit = 0;
        }
        else if (type == "app")
        {
            if (!cfg.Rules.Apps.Remove(value))
            {
                Console.WriteLine(Colorize("App not found", "yellow"));
                return;
            }
        }
        else
        {
            Console.WriteLine(Colorize("Unknown rule type", "red"));
            return;
        }
        SaveConfig(cfg);
        Console.WriteLine(Colorize($"Rule removed: {type} -> {value}", "green"));
    }

    static void ListRules()
    {
        var cfg = LoadConfig();
        Console.WriteLine(Colorize("Current rules:", "blue"));
        if (cfg.Rules.Sites.Any())
        {
            Console.WriteLine(Colorize("  Blocked sites:", "yellow"));
            foreach (var s in cfg.Rules.Sites) Console.WriteLine($"    - {s}");
        }
        if (cfg.Rules.TimeLimit > 0)
            Console.WriteLine(Colorize($"  Time limit: {cfg.Rules.TimeLimit} min", "yellow"));
        if (cfg.Rules.Apps.Any())
        {
            Console.WriteLine(Colorize("  Blocked apps:", "yellow"));
            foreach (var a in cfg.Rules.Apps) Console.WriteLine($"    - {a}");
        }
        if (!cfg.Rules.Sites.Any() && cfg.Rules.TimeLimit == 0 && !cfg.Rules.Apps.Any())
            Console.WriteLine(Colorize("  No rules.", "yellow"));
    }

    static void EnableControl()
    {
        var cfg = LoadConfig();
        if (cfg.Active)
        {
            Console.WriteLine(Colorize("Parent control already enabled.", "yellow"));
            return;
        }
        cfg.Active = true;
        cfg.StartTime = DateTime.Now.ToString("o");
        SaveConfig(cfg);
        if (cfg.Rules.Sites.Any()) UpdateHosts(cfg.Rules.Sites, "add");
        Console.WriteLine(Colorize("Parent control enabled.", "green"));
    }

    static void DisableControl()
    {
        var cfg = LoadConfig();
        if (!cfg.Active)
        {
            Console.WriteLine(Colorize("Parent control already disabled.", "yellow"));
            return;
        }
        cfg.Active = false;
        if (cfg.Rules.Sites.Any()) UpdateHosts(cfg.Rules.Sites, "remove");
        SaveConfig(cfg);
        Console.WriteLine(Colorize("Parent control disabled.", "green"));
    }

    static void ShowStatus()
    {
        var cfg = LoadConfig();
        Console.WriteLine(Colorize($"Parent control: {(cfg.Active ? "ENABLED" : "DISABLED")}", "blue"));
        if (cfg.Active && !string.IsNullOrEmpty(cfg.StartTime))
        {
            var start = DateTime.Parse(cfg.StartTime);
            var elapsed = (int)(DateTime.Now - start).TotalMinutes;
            Console.WriteLine(Colorize($"Session time: {elapsed} min", "yellow"));
            if (cfg.Rules.TimeLimit > 0)
                Console.WriteLine(Colorize($"Time limit: {cfg.Rules.TimeLimit} min", "yellow"));
        }
        Console.WriteLine(Colorize($"Blocked sites: {cfg.Rules.Sites.Count}", "yellow"));
        Console.WriteLine(Colorize($"Blocked apps: {cfg.Rules.Apps.Count}", "yellow"));
    }

    static void GenerateReport()
    {
        var cfg = LoadConfig();
        Console.WriteLine(Colorize("Activity report:", "blue"));
        Console.WriteLine($"  Date: {DateTime.Now:yyyy-MM-dd}");
        Console.WriteLine($"  Total time: {cfg.TotalTime} min");
        Console.WriteLine($"  Time limit: {cfg.Rules.TimeLimit} min");
        Console.WriteLine($"  Blocked sites: {cfg.Rules.Sites.Count}");
        Console.WriteLine($"  Blocked apps: {cfg.Rules.Apps.Count}");
    }

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine(Colorize("Usage: parent_control add|remove|list|enable|disable|status|report [args...]", "yellow"));
            return;
        }
        string cmd = args[0];
        var rest = args.Skip(1).ToList();

        switch (cmd)
        {
            case "add":
                if (rest.Count < 2) { Console.WriteLine(Colorize("Usage: add <site|time|app> <value>", "yellow")); return; }
                AddRule(rest[0], rest[1]);
                break;
            case "remove":
                if (rest.Count < 2) { Console.WriteLine(Colorize("Usage: remove <site|time|app> <value>", "yellow")); return; }
                RemoveRule(rest[0], rest[1]);
                break;
            case "list":
                ListRules();
                break;
            case "enable":
                EnableControl();
                break;
            case "disable":
                DisableControl();
                break;
            case "status":
                ShowStatus();
                break;
            case "report":
                GenerateReport();
                break;
            default:
                Console.WriteLine(Colorize($"Unknown command: {cmd}", "red"));
                break;
        }
    }
}

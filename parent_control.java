// parent_control.java
import java.io.*;
import java.nio.file.*;
import java.time.*;
import java.time.format.*;
import java.util.*;
import java.util.stream.*;
import com.google.gson.*;

public class parent_control {
    private static final String RESET = "\u001B[0m";
    private static final String GREEN = "\u001B[92m";
    private static final String RED = "\u001B[91m";
    private static final String YELLOW = "\u001B[93m";
    private static final String BLUE = "\u001B[94m";

    private static String colorize(String text, String color) {
        return color + text + RESET;
    }

    private static class Rules {
        List<String> sites = new ArrayList<>();
        int timeLimit;
        List<String> apps = new ArrayList<>();
    }

    private static class Config {
        Rules rules = new Rules();
        boolean active;
        String startTime;
        int totalTime;
    }

    private static String configDir = System.getProperty("user.home") + "/.parent_control";
    private static String configFile = configDir + "/config.json";
    private static String hostsFile = System.getProperty("os.name").toLowerCase().contains("win")
        ? "C:\\Windows\\System32\\drivers\\etc\\hosts"
        : "/etc/hosts";
    private static final String BLOCK_IP = "127.0.0.1";

    private static void ensureConfig() throws IOException {
        Files.createDirectories(Paths.get(configDir));
        if (!Files.exists(Paths.get(configFile))) {
            Config def = new Config();
            saveConfig(def);
        }
    }

    private static Config loadConfig() throws IOException {
        ensureConfig();
        String json = new String(Files.readAllBytes(Paths.get(configFile)));
        Gson gson = new Gson();
        return gson.fromJson(json, Config.class);
    }

    private static void saveConfig(Config cfg) throws IOException {
        Gson gson = new GsonBuilder().setPrettyPrinting().create();
        String json = gson.toJson(cfg);
        Files.write(Paths.get(configFile), json.getBytes());
    }

    private static List<String> readHosts() throws IOException {
        return Files.readAllLines(Paths.get(hostsFile));
    }

    private static void writeHosts(List<String> lines) throws IOException {
        Files.write(Paths.get(hostsFile), lines);
    }

    private static void updateHosts(List<String> domains, String action) throws IOException {
        List<String> lines = readHosts();
        List<String> newLines = lines.stream()
            .filter(line -> domains.stream().noneMatch(d -> line.trim().startsWith(BLOCK_IP) && line.contains(d)))
            .collect(Collectors.toList());
        if (action.equals("add")) {
            for (String d : domains) newLines.add(BLOCK_IP + " " + d);
        }
        writeHosts(newLines);
    }

    private static void addRule(String type, String value) throws IOException {
        Config cfg = loadConfig();
        if (type.equals("site")) {
            if (!cfg.rules.sites.contains(value)) {
                cfg.rules.sites.add(value);
                updateHosts(List.of(value), "add");
            } else {
                System.out.println(colorize("Site already in list", YELLOW));
                return;
            }
        } else if (type.equals("time")) {
            int mins;
            try { mins = Integer.parseInt(value); } catch (NumberFormatException e) {
                System.out.println(colorize("Invalid time value", RED));
                return;
            }
            cfg.rules.timeLimit = mins;
        } else if (type.equals("app")) {
            if (!cfg.rules.apps.contains(value)) {
                cfg.rules.apps.add(value);
            } else {
                System.out.println(colorize("App already in list", YELLOW));
                return;
            }
        } else {
            System.out.println(colorize("Unknown rule type", RED));
            return;
        }
        saveConfig(cfg);
        System.out.println(colorize("Rule added: " + type + " -> " + value, GREEN));
    }

    private static void removeRule(String type, String value) throws IOException {
        Config cfg = loadConfig();
        if (type.equals("site")) {
            if (cfg.rules.sites.remove(value)) {
                updateHosts(List.of(value), "remove");
            } else {
                System.out.println(colorize("Site not found", YELLOW));
                return;
            }
        } else if (type.equals("time")) {
            cfg.rules.timeLimit = 0;
        } else if (type.equals("app")) {
            if (!cfg.rules.apps.remove(value)) {
                System.out.println(colorize("App not found", YELLOW));
                return;
            }
        } else {
            System.out.println(colorize("Unknown rule type", RED));
            return;
        }
        saveConfig(cfg);
        System.out.println(colorize("Rule removed: " + type + " -> " + value, GREEN));
    }

    private static void listRules() throws IOException {
        Config cfg = loadConfig();
        System.out.println(colorize("Current rules:", BLUE));
        if (!cfg.rules.sites.isEmpty()) {
            System.out.println(colorize("  Blocked sites:", YELLOW));
            for (String s : cfg.rules.sites) System.out.println("    - " + s);
        }
        if (cfg.rules.timeLimit > 0)
            System.out.println(colorize("  Time limit: " + cfg.rules.timeLimit + " min", YELLOW));
        if (!cfg.rules.apps.isEmpty()) {
            System.out.println(colorize("  Blocked apps:", YELLOW));
            for (String a : cfg.rules.apps) System.out.println("    - " + a);
        }
        if (cfg.rules.sites.isEmpty() && cfg.rules.timeLimit == 0 && cfg.rules.apps.isEmpty())
            System.out.println(colorize("  No rules.", YELLOW));
    }

    private static void enableControl() throws IOException {
        Config cfg = loadConfig();
        if (cfg.active) {
            System.out.println(colorize("Parent control already enabled.", YELLOW));
            return;
        }
        cfg.active = true;
        cfg.startTime = Instant.now().toString();
        saveConfig(cfg);
        if (!cfg.rules.sites.isEmpty()) updateHosts(cfg.rules.sites, "add");
        System.out.println(colorize("Parent control enabled.", GREEN));
    }

    private static void disableControl() throws IOException {
        Config cfg = loadConfig();
        if (!cfg.active) {
            System.out.println(colorize("Parent control already disabled.", YELLOW));
            return;
        }
        cfg.active = false;
        if (!cfg.rules.sites.isEmpty()) updateHosts(cfg.rules.sites, "remove");
        saveConfig(cfg);
        System.out.println(colorize("Parent control disabled.", GREEN));
    }

    private static void showStatus() throws IOException {
        Config cfg = loadConfig();
        System.out.println(colorize("Parent control: " + (cfg.active ? "ENABLED" : "DISABLED"), BLUE));
        if (cfg.active && cfg.startTime != null) {
            Instant start = Instant.parse(cfg.startTime);
            long elapsed = Duration.between(start, Instant.now()).toMinutes();
            System.out.println(colorize("Session time: " + elapsed + " min", YELLOW));
            if (cfg.rules.timeLimit > 0)
                System.out.println(colorize("Time limit: " + cfg.rules.timeLimit + " min", YELLOW));
        }
        System.out.println(colorize("Blocked sites: " + cfg.rules.sites.size(), YELLOW));
        System.out.println(colorize("Blocked apps: " + cfg.rules.apps.size(), YELLOW));
    }

    private static void generateReport() throws IOException {
        Config cfg = loadConfig();
        System.out.println(colorize("Activity report:", BLUE));
        System.out.println("  Date: " + LocalDate.now());
        System.out.println("  Total time: " + cfg.totalTime + " min");
        System.out.println("  Time limit: " + cfg.rules.timeLimit + " min");
        System.out.println("  Blocked sites: " + cfg.rules.sites.size());
        System.out.println("  Blocked apps: " + cfg.rules.apps.size());
    }

    public static void main(String[] args) throws IOException {
        if (args.length < 1) {
            System.out.println(colorize("Usage: parent_control add|remove|list|enable|disable|status|report [args...]", YELLOW));
            return;
        }
        String cmd = args[0];
        List<String> rest = Arrays.asList(args).subList(1, args.length);

        switch (cmd) {
            case "add":
                if (rest.size() < 2) { System.out.println(colorize("Usage: add <site|time|app> <value>", YELLOW)); return; }
                addRule(rest.get(0), rest.get(1));
                break;
            case "remove":
                if (rest.size() < 2) { System.out.println(colorize("Usage: remove <site|time|app> <value>", YELLOW)); return; }
                removeRule(rest.get(0), rest.get(1));
                break;
            case "list":
                listRules();
                break;
            case "enable":
                enableControl();
                break;
            case "disable":
                disableControl();
                break;
            case "status":
                showStatus();
                break;
            case "report":
                generateReport();
                break;
            default:
                System.out.println(colorize("Unknown command: " + cmd, RED));
        }
    }
}

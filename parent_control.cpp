// parent_control.cpp
#include <iostream>
#include <string>
#include <vector>
#include <map>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <ctime>
#include <json/json.h> // sudo apt-get install libjsoncpp-dev

using namespace std;
namespace fs = std::filesystem;

const string RESET = "\033[0m";
const string GREEN = "\033[92m";
const string RED = "\033[91m";
const string YELLOW = "\033[93m";
const string BLUE = "\033[94m";

string colorize(const string& text, const string& color) {
    return color + text + RESET;
}

string getConfigDir() {
    const char* home = getenv("HOME");
    if (!home) home = getenv("USERPROFILE");
    return string(home) + "/.parent_control";
}

string getConfigFile() {
    return getConfigDir() + "/config.json";
}

string getHostsFile() {
#ifdef _WIN32
    return "C:\\Windows\\System32\\drivers\\etc\\hosts";
#else
    return "/etc/hosts";
#endif
}

const string BLOCK_IP = "127.0.0.1";

Json::Value loadConfig() {
    fs::create_directories(getConfigDir());
    ifstream f(getConfigFile());
    Json::Value root;
    if (!f) {
        root["rules"]["sites"] = Json::arrayValue;
        root["rules"]["time_limit"] = 0;
        root["rules"]["apps"] = Json::arrayValue;
        root["active"] = false;
        root["start_time"] = Json::nullValue;
        root["total_time"] = 0;
        return root;
    }
    f >> root;
    return root;
}

void saveConfig(const Json::Value& root) {
    ofstream f(getConfigFile());
    f << root.toStyledString();
}

vector<string> readHosts() {
    ifstream f(getHostsFile());
    vector<string> lines;
    string line;
    while (getline(f, line)) lines.push_back(line);
    return lines;
}

void writeHosts(const vector<string>& lines) {
    ofstream f(getHostsFile());
    for (const auto& l : lines) f << l << endl;
}

void updateHosts(const vector<string>& domains, const string& action) {
    auto lines = readHosts();
    vector<string> newLines;
    for (const auto& line : lines) {
        bool skip = false;
        for (const auto& d : domains) {
            if (line.find(BLOCK_IP) == 0 && line.find(d) != string::npos) {
                skip = true;
                break;
            }
        }
        if (!skip) newLines.push_back(line);
    }
    if (action == "add") {
        for (const auto& d : domains) {
            newLines.push_back(BLOCK_IP + " " + d);
        }
    }
    writeHosts(newLines);
}

void addRule(const string& type, const string& value) {
    auto root = loadConfig();
    if (type == "site") {
        bool exists = false;
        for (const auto& s : root["rules"]["sites"]) {
            if (s.asString() == value) exists = true;
        }
        if (!exists) {
            root["rules"]["sites"].append(value);
            updateHosts({value}, "add");
        }
    } else if (type == "time") {
        int minutes = stoi(value);
        root["rules"]["time_limit"] = minutes;
    } else if (type == "app") {
        bool exists = false;
        for (const auto& a : root["rules"]["apps"]) {
            if (a.asString() == value) exists = true;
        }
        if (!exists) root["rules"]["apps"].append(value);
    } else {
        cout << colorize("Unknown rule type", RED) << endl;
        return;
    }
    saveConfig(root);
    cout << colorize("Rule added: " + type + " -> " + value, GREEN) << endl;
}

void removeRule(const string& type, const string& value) {
    auto root = loadConfig();
    if (type == "site") {
        Json::Value newSites = Json::arrayValue;
        for (const auto& s : root["rules"]["sites"]) {
            if (s.asString() != value) newSites.append(s);
        }
        root["rules"]["sites"] = newSites;
        updateHosts({value}, "remove");
    } else if (type == "time") {
        root["rules"]["time_limit"] = 0;
    } else if (type == "app") {
        Json::Value newApps = Json::arrayValue;
        for (const auto& a : root["rules"]["apps"]) {
            if (a.asString() != value) newApps.append(a);
        }
        root["rules"]["apps"] = newApps;
    } else {
        cout << colorize("Unknown rule type", RED) << endl;
        return;
    }
    saveConfig(root);
    cout << colorize("Rule removed: " + type + " -> " + value, GREEN) << endl;
}

void listRules() {
    auto root = loadConfig();
    cout << colorize("Current rules:", BLUE) << endl;
    if (root["rules"]["sites"].size() > 0) {
        cout << colorize("  Blocked sites:", YELLOW) << endl;
        for (const auto& s : root["rules"]["sites"])
            cout << "    - " << s.asString() << endl;
    }
    int timeLimit = root["rules"]["time_limit"].asInt();
    if (timeLimit > 0)
        cout << colorize("  Time limit: " + to_string(timeLimit) + " min", YELLOW) << endl;
    if (root["rules"]["apps"].size() > 0) {
        cout << colorize("  Blocked apps:", YELLOW) << endl;
        for (const auto& a : root["rules"]["apps"])
            cout << "    - " << a.asString() << endl;
    }
    if (root["rules"]["sites"].size() == 0 && timeLimit == 0 && root["rules"]["apps"].size() == 0)
        cout << colorize("  No rules.", YELLOW) << endl;
}

void enableControl() {
    auto root = loadConfig();
    if (root["active"].asBool()) {
        cout << colorize("Parent control already enabled.", YELLOW) << endl;
        return;
    }
    root["active"] = true;
    time_t now = time(nullptr);
    char buf[64];
    strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%S", localtime(&now));
    root["start_time"] = string(buf);
    saveConfig(root);
    // Apply site blocks
    vector<string> sites;
    for (const auto& s : root["rules"]["sites"]) sites.push_back(s.asString());
    if (!sites.empty()) updateHosts(sites, "add");
    cout << colorize("Parent control enabled.", GREEN) << endl;
}

void disableControl() {
    auto root = loadConfig();
    if (!root["active"].asBool()) {
        cout << colorize("Parent control already disabled.", YELLOW) << endl;
        return;
    }
    root["active"] = false;
    vector<string> sites;
    for (const auto& s : root["rules"]["sites"]) sites.push_back(s.asString());
    if (!sites.empty()) updateHosts(sites, "remove");
    saveConfig(root);
    cout << colorize("Parent control disabled.", GREEN) << endl;
}

void showStatus() {
    auto root = loadConfig();
    cout << colorize("Parent control: " + string(root["active"].asBool() ? "ENABLED" : "DISABLED"), BLUE) << endl;
    if (root["active"].asBool() && root.isMember("start_time") && !root["start_time"].isNull()) {
        string startStr = root["start_time"].asString();
        tm tm = {};
        stringstream ss(startStr);
        ss >> get_time(&tm, "%Y-%m-%dT%H:%M:%S");
        time_t start = mktime(&tm);
        int elapsed = (int)difftime(time(nullptr), start) / 60;
        cout << colorize("Session time: " + to_string(elapsed) + " min", YELLOW) << endl;
        int limit = root["rules"]["time_limit"].asInt();
        if (limit > 0) cout << colorize("Time limit: " + to_string(limit) + " min", YELLOW) << endl;
    }
    cout << colorize("Blocked sites: " + to_string(root["rules"]["sites"].size()), YELLOW) << endl;
    cout << colorize("Blocked apps: " + to_string(root["rules"]["apps"].size()), YELLOW) << endl;
}

void generateReport() {
    auto root = loadConfig();
    cout << colorize("Activity report:", BLUE) << endl;
    cout << "  Date: " << time(nullptr) << endl; // упрощённо
    cout << "  Total time: " << root["total_time"].asInt() << " min" << endl;
    cout << "  Time limit: " << root["rules"]["time_limit"].asInt() << " min" << endl;
    cout << "  Blocked sites: " << root["rules"]["sites"].size() << endl;
    cout << "  Blocked apps: " << root["rules"]["apps"].size() << endl;
}

int main(int argc, char* argv[]) {
    if (argc < 2) {
        cout << colorize("Usage: parent_control add|remove|list|enable|disable|status|report [args...]", YELLOW) << endl;
        return 1;
    }
    string cmd = argv[1];
    vector<string> args;
    for (int i = 2; i < argc; ++i) args.push_back(argv[i]);

    if (cmd == "add") {
        if (args.size() < 2) {
            cout << colorize("Usage: add <site|time|app> <value>", YELLOW) << endl;
            return 1;
        }
        addRule(args[0], args[1]);
    } else if (cmd == "remove") {
        if (args.size() < 2) {
            cout << colorize("Usage: remove <site|time|app> <value>", YELLOW) << endl;
            return 1;
        }
        removeRule(args[0], args[1]);
    } else if (cmd == "list") {
        listRules();
    } else if (cmd == "enable") {
        enableControl();
    } else if (cmd == "disable") {
        disableControl();
    } else if (cmd == "status") {
        showStatus();
    } else if (cmd == "report") {
        generateReport();
    } else {
        cout << colorize("Unknown command: " + cmd, RED) << endl;
    }
    return 0;
}

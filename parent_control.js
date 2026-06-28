// parent_control.js
#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const os = require('os');
const { execSync } = require('child_process');

const COLORS = {
    green: '\x1b[92m',
    red: '\x1b[91m',
    yellow: '\x1b[93m',
    blue: '\x1b[94m',
    reset: '\x1b[0m'
};

function colorize(text, color) {
    return COLORS[color] + text + COLORS.reset;
}

const CONFIG_DIR = path.join(os.homedir(), '.parent_control');
const CONFIG_FILE = path.join(CONFIG_DIR, 'config.json');
const HOSTS_FILE = os.platform() === 'win32'
    ? 'C:\\Windows\\System32\\drivers\\etc\\hosts'
    : '/etc/hosts';
const BLOCK_IP = '127.0.0.1';

function ensureConfig() {
    if (!fs.existsSync(CONFIG_DIR)) fs.mkdirSync(CONFIG_DIR, { recursive: true });
    if (!fs.existsSync(CONFIG_FILE)) {
        const defaultCfg = {
            rules: { sites: [], time_limit: 0, apps: [] },
            active: false,
            start_time: null,
            total_time: 0
        };
        fs.writeFileSync(CONFIG_FILE, JSON.stringify(defaultCfg, null, 2));
    }
}

function loadConfig() {
    ensureConfig();
    return JSON.parse(fs.readFileSync(CONFIG_FILE, 'utf8'));
}

function saveConfig(cfg) {
    fs.writeFileSync(CONFIG_FILE, JSON.stringify(cfg, null, 2));
}

function readHosts() {
    return fs.readFileSync(HOSTS_FILE, 'utf8').split('\n');
}

function writeHosts(lines) {
    fs.writeFileSync(HOSTS_FILE, lines.join('\n'));
}

function updateHosts(domains, action) {
    const lines = readHosts();
    const newLines = lines.filter(line => {
        for (const d of domains) {
            if (line.trim().startsWith(BLOCK_IP) && line.includes(d)) return false;
        }
        return true;
    });
    if (action === 'add') {
        for (const d of domains) newLines.push(`${BLOCK_IP} ${d}`);
    }
    writeHosts(newLines);
}

function addRule(type, value) {
    const cfg = loadConfig();
    if (type === 'site') {
        if (!cfg.rules.sites.includes(value)) {
            cfg.rules.sites.push(value);
            updateHosts([value], 'add');
        } else {
            console.log(colorize('Site already in list', 'yellow'));
            return;
        }
    } else if (type === 'time') {
        const mins = parseInt(value, 10);
        if (isNaN(mins)) { console.log(colorize('Invalid time value', 'red')); return; }
        cfg.rules.time_limit = mins;
    } else if (type === 'app') {
        if (!cfg.rules.apps.includes(value)) {
            cfg.rules.apps.push(value);
        } else {
            console.log(colorize('App already in list', 'yellow'));
            return;
        }
    } else {
        console.log(colorize('Unknown rule type', 'red'));
        return;
    }
    saveConfig(cfg);
    console.log(colorize(`Rule added: ${type} -> ${value}`, 'green'));
}

function removeRule(type, value) {
    const cfg = loadConfig();
    if (type === 'site') {
        const idx = cfg.rules.sites.indexOf(value);
        if (idx !== -1) {
            cfg.rules.sites.splice(idx, 1);
            updateHosts([value], 'remove');
        } else {
            console.log(colorize('Site not found', 'yellow'));
            return;
        }
    } else if (type === 'time') {
        cfg.rules.time_limit = 0;
    } else if (type === 'app') {
        const idx = cfg.rules.apps.indexOf(value);
        if (idx !== -1) {
            cfg.rules.apps.splice(idx, 1);
        } else {
            console.log(colorize('App not found', 'yellow'));
            return;
        }
    } else {
        console.log(colorize('Unknown rule type', 'red'));
        return;
    }
    saveConfig(cfg);
    console.log(colorize(`Rule removed: ${type} -> ${value}`, 'green'));
}

function listRules() {
    const cfg = loadConfig();
    console.log(colorize('Current rules:', 'blue'));
    if (cfg.rules.sites.length) {
        console.log(colorize('  Blocked sites:', 'yellow'));
        cfg.rules.sites.forEach(s => console.log(`    - ${s}`));
    }
    if (cfg.rules.time_limit) {
        console.log(colorize(`  Time limit: ${cfg.rules.time_limit} min`, 'yellow'));
    }
    if (cfg.rules.apps.length) {
        console.log(colorize('  Blocked apps:', 'yellow'));
        cfg.rules.apps.forEach(a => console.log(`    - ${a}`));
    }
    if (!cfg.rules.sites.length && !cfg.rules.time_limit && !cfg.rules.apps.length) {
        console.log(colorize('  No rules.', 'yellow'));
    }
}

function enableControl() {
    const cfg = loadConfig();
    if (cfg.active) {
        console.log(colorize('Parent control already enabled.', 'yellow'));
        return;
    }
    cfg.active = true;
    cfg.start_time = new Date().toISOString();
    saveConfig(cfg);
    if (cfg.rules.sites.length) updateHosts(cfg.rules.sites, 'add');
    console.log(colorize('Parent control enabled.', 'green'));
}

function disableControl() {
    const cfg = loadConfig();
    if (!cfg.active) {
        console.log(colorize('Parent control already disabled.', 'yellow'));
        return;
    }
    cfg.active = false;
    if (cfg.rules.sites.length) updateHosts(cfg.rules.sites, 'remove');
    saveConfig(cfg);
    console.log(colorize('Parent control disabled.', 'green'));
}

function showStatus() {
    const cfg = loadConfig();
    console.log(colorize(`Parent control: ${cfg.active ? 'ENABLED' : 'DISABLED'}`, 'blue'));
    if (cfg.active && cfg.start_time) {
        const start = new Date(cfg.start_time);
        const elapsed = Math.floor((Date.now() - start) / 60000);
        console.log(colorize(`Session time: ${elapsed} min`, 'yellow'));
        if (cfg.rules.time_limit) {
            console.log(colorize(`Time limit: ${cfg.rules.time_limit} min`, 'yellow'));
        }
    }
    console.log(colorize(`Blocked sites: ${cfg.rules.sites.length}`, 'yellow'));
    console.log(colorize(`Blocked apps: ${cfg.rules.apps.length}`, 'yellow'));
}

function generateReport() {
    const cfg = loadConfig();
    console.log(colorize('Activity report:', 'blue'));
    console.log(`  Date: ${new Date().toISOString().slice(0,10)}`);
    console.log(`  Total time: ${cfg.total_time} min`);
    console.log(`  Time limit: ${cfg.rules.time_limit} min`);
    console.log(`  Blocked sites: ${cfg.rules.sites.length}`);
    console.log(`  Blocked apps: ${cfg.rules.apps.length}`);
}

function main() {
    const args = process.argv.slice(2);
    if (args.length < 1) {
        console.log(colorize('Usage: parent_control add|remove|list|enable|disable|status|report [args...]', 'yellow'));
        process.exit(1);
    }
    const cmd = args[0];
    const rest = args.slice(1);

    switch (cmd) {
        case 'add':
            if (rest.length < 2) {
                console.log(colorize('Usage: add <site|time|app> <value>', 'yellow'));
                return;
            }
            addRule(rest[0], rest[1]);
            break;
        case 'remove':
            if (rest.length < 2) {
                console.log(colorize('Usage: remove <site|time|app> <value>', 'yellow'));
                return;
            }
            removeRule(rest[0], rest[1]);
            break;
        case 'list':
            listRules();
            break;
        case 'enable':
            enableControl();
            break;
        case 'disable':
            disableControl();
            break;
        case 'status':
            showStatus();
            break;
        case 'report':
            generateReport();
            break;
        default:
            console.log(colorize(`Unknown command: ${cmd}`, 'red'));
    }
}

main();

# parent_control.py
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys
import os
import json
import time
import argparse
import subprocess
import threading
from datetime import datetime, timedelta
from pathlib import Path

# ANSI colors
COLORS = {
    'green': '\033[92m',
    'red': '\033[91m',
    'yellow': '\033[93m',
    'blue': '\033[94m',
    'reset': '\033[0m'
}

def colorize(text, color):
    return f"{COLORS.get(color, '')}{text}{COLORS['reset']}"

CONFIG_DIR = Path.home() / '.parent_control'
CONFIG_FILE = CONFIG_DIR / 'config.json'
HOSTS_FILE = '/etc/hosts' if os.name != 'nt' else r'C:\Windows\System32\drivers\etc\hosts'
BLOCK_IP = '127.0.0.1'

def ensure_config():
    CONFIG_DIR.mkdir(exist_ok=True)
    if not CONFIG_FILE.exists():
        default = {
            'rules': {'sites': [], 'time_limit': 0, 'apps': []},
            'active': False,
            'start_time': None,
            'total_time': 0
        }
        with open(CONFIG_FILE, 'w') as f:
            json.dump(default, f, indent=2)

def load_config():
    ensure_config()
    with open(CONFIG_FILE, 'r') as f:
        return json.load(f)

def save_config(config):
    with open(CONFIG_FILE, 'w') as f:
        json.dump(config, f, indent=2)

def read_hosts():
    with open(HOSTS_FILE, 'r') as f:
        return f.readlines()

def write_hosts(lines):
    with open(HOSTS_FILE, 'w') as f:
        f.writelines(lines)

def update_hosts(domains, action):
    lines = read_hosts()
    new_lines = []
    for line in lines:
        if any(line.strip().startswith(BLOCK_IP) and domain in line for domain in domains):
            continue
        new_lines.append(line)
    if action == 'add':
        for domain in domains:
            new_lines.append(f"{BLOCK_IP} {domain}\n")
    write_hosts(new_lines)

def add_rule(rule_type, value):
    config = load_config()
    if rule_type == 'site':
        if value not in config['rules']['sites']:
            config['rules']['sites'].append(value)
            # Обновляем hosts
            try:
                update_hosts([value], 'add')
            except Exception as e:
                print(colorize(f"Ошибка обновления hosts: {e}", 'red'))
    elif rule_type == 'time':
        try:
            minutes = int(value)
            config['rules']['time_limit'] = minutes
        except ValueError:
            print(colorize("Ошибка: время должно быть числом (минуты)", 'red'))
            return
    elif rule_type == 'app':
        if value not in config['rules']['apps']:
            config['rules']['apps'].append(value)
    else:
        print(colorize("Неизвестный тип правила", 'red'))
        return
    save_config(config)
    print(colorize(f"Правило добавлено: {rule_type} -> {value}", 'green'))

def remove_rule(rule_type, value):
    config = load_config()
    if rule_type == 'site':
        if value in config['rules']['sites']:
            config['rules']['sites'].remove(value)
            try:
                update_hosts([value], 'remove')
            except Exception as e:
                print(colorize(f"Ошибка обновления hosts: {e}", 'red'))
        else:
            print(colorize("Сайт не найден в списке", 'yellow'))
            return
    elif rule_type == 'time':
        config['rules']['time_limit'] = 0
    elif rule_type == 'app':
        if value in config['rules']['apps']:
            config['rules']['apps'].remove(value)
        else:
            print(colorize("Приложение не найдено в списке", 'yellow'))
            return
    else:
        print(colorize("Неизвестный тип правила", 'red'))
        return
    save_config(config)
    print(colorize(f"Правило удалено: {rule_type} -> {value}", 'green'))

def list_rules():
    config = load_config()
    rules = config['rules']
    print(colorize("Текущие правила:", 'blue'))
    if rules['sites']:
        print(colorize("  Заблокированные сайты:", 'yellow'))
        for s in rules['sites']:
            print(f"    - {s}")
    if rules['time_limit'] > 0:
        print(colorize(f"  Лимит времени: {rules['time_limit']} мин", 'yellow'))
    if rules['apps']:
        print(colorize("  Запрещённые приложения:", 'yellow'))
        for a in rules['apps']:
            print(f"    - {a}")
    if not any(rules.values()):
        print(colorize("  Нет правил.", 'yellow'))

def enable_control():
    config = load_config()
    if config['active']:
        print(colorize("Родительский контроль уже включён.", 'yellow'))
        return
    config['active'] = True
    config['start_time'] = datetime.now().isoformat()
    save_config(config)
    # Применяем блокировки
    sites = config['rules']['sites']
    if sites:
        try:
            update_hosts(sites, 'add')
        except Exception as e:
            print(colorize(f"Ошибка обновления hosts: {e}", 'red'))
    print(colorize("Родительский контроль включён.", 'green'))
    # Запускаем фоновый мониторинг
    threading.Thread(target=monitor_loop, daemon=True).start()

def disable_control():
    config = load_config()
    if not config['active']:
        print(colorize("Родительский контроль уже выключён.", 'yellow'))
        return
    config['active'] = False
    # Снимаем блокировки
    sites = config['rules']['sites']
    if sites:
        try:
            update_hosts(sites, 'remove')
        except Exception as e:
            print(colorize(f"Ошибка обновления hosts: {e}", 'red'))
    save_config(config)
    print(colorize("Родительский контроль выключён.", 'green'))

def monitor_loop():
    """Фоновый мониторинг времени и приложений."""
    config = load_config()
    while config['active']:
        # Проверяем лимит времени
        if config['rules']['time_limit'] > 0:
            total = config['total_time']
            # Сброс в полночь
            now = datetime.now()
            if now.hour == 0 and now.minute == 0:
                config['total_time'] = 0
                total = 0
            # В реальном проекте нужно увеличивать total_time каждую минуту
            # Здесь для демонстрации просто проверяем, не превышен ли лимит
            # В настоящей реализации нужно вести учёт времени через системные логи или периодический опрос
            # Для теста используем заглушку
            pass
        # Проверка приложений (для теста просто имитируем)
        # В реальном проекте используется psutil или аналоги
        time.sleep(60)  # проверка раз в минуту
        config = load_config()

def show_status():
    config = load_config()
    active = config['active']
    print(colorize(f"Родительский контроль: {'ВКЛЮЧЁН' if active else 'ВЫКЛЮЧЁН'}", 'blue'))
    if active:
        start = config.get('start_time')
        if start:
            dt = datetime.fromisoformat(start)
            elapsed = (datetime.now() - dt).total_seconds() // 60
            print(colorize(f"Время работы с начала сессии: {elapsed} мин", 'yellow'))
        if config['rules']['time_limit'] > 0:
            print(colorize(f"Установленный лимит: {config['rules']['time_limit']} мин", 'yellow'))
        sites = config['rules']['sites']
        if sites:
            print(colorize(f"Заблокировано сайтов: {len(sites)}", 'yellow'))
        apps = config['rules']['apps']
        if apps:
            print(colorize(f"Запрещено приложений: {len(apps)}", 'yellow'))

def generate_report():
    config = load_config()
    print(colorize("Отчёт по активности:", 'blue'))
    print(f"  Дата: {datetime.now().strftime('%Y-%m-%d')}")
    total = config['total_time']
    print(f"  Общее время работы: {total} мин")
    print(f"  Лимит времени: {config['rules']['time_limit']} мин")
    print(f"  Заблокированные сайты: {len(config['rules']['sites'])}")
    print(f"  Запрещённые приложения: {len(config['rules']['apps'])}")
    # Здесь можно добавить историю посещений и запусков, но для теста достаточно.

def main():
    if len(sys.argv) < 2:
        print(colorize("Usage: parent_control.py <add|remove|list|enable|disable|status|report> [args...]", 'yellow'))
        sys.exit(1)

    cmd = sys.argv[1].lower()
    args = sys.argv[2:]

    if cmd == 'add':
        if len(args) < 2:
            print(colorize("Usage: add <site|time|app> <value>", 'yellow'))
            return
        rule_type, value = args[0], args[1]
        add_rule(rule_type, value)
    elif cmd == 'remove':
        if len(args) < 2:
            print(colorize("Usage: remove <site|time|app> <value>", 'yellow'))
            return
        rule_type, value = args[0], args[1]
        remove_rule(rule_type, value)
    elif cmd == 'list':
        list_rules()
    elif cmd == 'enable':
        enable_control()
    elif cmd == 'disable':
        disable_control()
    elif cmd == 'status':
        show_status()
    elif cmd == 'report':
        generate_report()
    else:
        print(colorize(f"Неизвестная команда: {cmd}", 'red'))

if __name__ == '__main__':
    main()

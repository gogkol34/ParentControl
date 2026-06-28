#!/usr/bin/env ruby
# parent_control.rb
# encoding: UTF-8

require 'json'
require 'fileutils'
require 'time'

COLORS = {
  green: "\e[92m",
  red: "\e[91m",
  yellow: "\e[93m",
  blue: "\e[94m",
  reset: "\e[0m"
}

def colorize(text, color)
  "#{COLORS[color]}#{text}#{COLORS[:reset]}"
end

CONFIG_DIR = File.join(Dir.home, '.parent_control')
CONFIG_FILE = File.join(CONFIG_DIR, 'config.json')
HOSTS_FILE = RUBY_PLATFORM =~ /mswin|mingw|cygwin/ ?
  'C:\Windows\System32\drivers\etc\hosts' : '/etc/hosts'
BLOCK_IP = '127.0.0.1'

def ensure_config
  FileUtils.mkdir_p(CONFIG_DIR)
  unless File.exist?(CONFIG_FILE)
    default = {
      'rules' => { 'sites' => [], 'time_limit' => 0, 'apps' => [] },
      'active' => false,
      'start_time' => nil,
      'total_time' => 0
    }
    File.write(CONFIG_FILE, JSON.pretty_generate(default))
  end
end

def load_config
  ensure_config
  JSON.parse(File.read(CONFIG_FILE))
end

def save_config(cfg)
  File.write(CONFIG_FILE, JSON.pretty_generate(cfg))
end

def read_hosts
  File.readlines(HOSTS_FILE).map(&:chomp)
rescue Errno::EACCES
  raise "Требуются права администратора для изменения hosts-файла"
end

def write_hosts(lines)
  File.write(HOSTS_FILE, lines.join("\n") + "\n")
end

def update_hosts(domains, action)
  lines = read_hosts
  new_lines = lines.reject do |line|
    domains.any? { |d| line.strip.start_with?(BLOCK_IP) && line.include?(d) }
  end
  if action == 'add'
    domains.each { |d| new_lines << "#{BLOCK_IP} #{d}" }
  end
  write_hosts(new_lines)
end

def add_rule(type, value)
  cfg = load_config
  case type
  when 'site'
    unless cfg['rules']['sites'].include?(value)
      cfg['rules']['sites'] << value
      update_hosts([value], 'add')
    else
      puts colorize("Site already in list", :yellow)
      return
    end
  when 'time'
    mins = value.to_i
    cfg['rules']['time_limit'] = mins
  when 'app'
    unless cfg['rules']['apps'].include?(value)
      cfg['rules']['apps'] << value
    else
      puts colorize("App already in list", :yellow)
      return
    end
  else
    puts colorize("Unknown rule type", :red)
    return
  end
  save_config(cfg)
  puts colorize("Rule added: #{type} -> #{value}", :green)
end

def remove_rule(type, value)
  cfg = load_config
  case type
  when 'site'
    if cfg['rules']['sites'].delete(value)
      update_hosts([value], 'remove')
    else
      puts colorize("Site not found", :yellow)
      return
    end
  when 'time'
    cfg['rules']['time_limit'] = 0
  when 'app'
    unless cfg['rules']['apps'].delete(value)
      puts colorize("App not found", :yellow)
      return
    end
  else
    puts colorize("Unknown rule type", :red)
    return
  end
  save_config(cfg)
  puts colorize("Rule removed: #{type} -> #{value}", :green)
end

def list_rules
  cfg = load_config
  puts colorize("Current rules:", :blue)
  if cfg['rules']['sites'].any?
    puts colorize("  Blocked sites:", :yellow)
    cfg['rules']['sites'].each { |s| puts "    - #{s}" }
  end
  if cfg['rules']['time_limit'] > 0
    puts colorize("  Time limit: #{cfg['rules']['time_limit']} min", :yellow)
  end
  if cfg['rules']['apps'].any?
    puts colorize("  Blocked apps:", :yellow)
    cfg['rules']['apps'].each { |a| puts "    - #{a}" }
  end
  if cfg['rules']['sites'].empty? && cfg['rules']['time_limit'] == 0 && cfg['rules']['apps'].empty?
    puts colorize("  No rules.", :yellow)
  end
end

def enable_control
  cfg = load_config
  if cfg['active']
    puts colorize("Parent control already enabled.", :yellow)
    return
  end
  cfg['active'] = true
  cfg['start_time'] = Time.now.iso8601
  save_config(cfg)
  if cfg['rules']['sites'].any?
    update_hosts(cfg['rules']['sites'], 'add')
  end
  puts colorize("Parent control enabled.", :green)
end

def disable_control
  cfg = load_config
  unless cfg['active']
    puts colorize("Parent control already disabled.", :yellow)
    return
  end
  cfg['active'] = false
  if cfg['rules']['sites'].any?
    update_hosts(cfg['rules']['sites'], 'remove')
  end
  save_config(cfg)
  puts colorize("Parent control disabled.", :green)
end

def show_status
  cfg = load_config
  puts colorize("Parent control: #{cfg['active'] ? 'ENABLED' : 'DISABLED'}", :blue)
  if cfg['active'] && cfg['start_time']
    start = Time.iso8601(cfg['start_time'])
    elapsed = ((Time.now - start) / 60).to_i
    puts colorize("Session time: #{elapsed} min", :yellow)
    if cfg['rules']['time_limit'] > 0
      puts colorize("Time limit: #{cfg['rules']['time_limit']} min", :yellow)
    end
  end
  puts colorize("Blocked sites: #{cfg['rules']['sites'].size}", :yellow)
  puts colorize("Blocked apps: #{cfg['rules']['apps'].size}", :yellow)
end

def generate_report
  cfg = load_config
  puts colorize("Activity report:", :blue)
  puts "  Date: #{Time.now.strftime('%Y-%m-%d')}"
  puts "  Total time: #{cfg['total_time']} min"
  puts "  Time limit: #{cfg['rules']['time_limit']} min"
  puts "  Blocked sites: #{cfg['rules']['sites'].size}"
  puts "  Blocked apps: #{cfg['rules']['apps'].size}"
end

def main
  if ARGV.empty?
    puts colorize("Usage: parent_control add|remove|list|enable|disable|status|report [args...]", :yellow)
    exit 1
  end

  cmd = ARGV[0]
  args = ARGV[1..-1] || []

  case cmd
  when 'add'
    if args.size < 2
      puts colorize("Usage: add <site|time|app> <value>", :yellow)
      return
    end
    add_rule(args[0], args[1])
  when 'remove'
    if args.size < 2
      puts colorize("Usage: remove <site|time|app> <value>", :yellow)
      return
    end
    remove_rule(args[0], args[1])
  when 'list'
    list_rules
  when 'enable'
    enable_control
  when 'disable'
    disable_control
  when 'status'
    show_status
  when 'report'
    generate_report
  else
    puts colorize("Unknown command: #{cmd}", :red)
  end
end

main if __FILE__ == $0

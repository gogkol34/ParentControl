👪 ParentControl – Родительский контроль на 7 языках
Комплексный инструмент для ограничения времени за компьютером, блокировки сайтов и контроля запуска приложений.
Позволяет устанавливать правила для пользователей, отслеживать активность и получать отчёты.
Реализован на семи языках программирования с единым интерфейсом командной строки.

🚀 Возможности
Управление правилами – добавление (add), удаление (remove), просмотр (list) правил.

Типы правил:

site – блокировка доступа к сайту (через файл hosts).

time – ограничение общего времени работы за компьютером (в минутах).

app – запрет запуска указанных приложений (по имени процесса).

Включение/отключение – команды enable и disable активируют/деактивируют все правила.

Статус – команда status показывает активные правила и оставшееся время.

Отчёт – команда report генерирует статистику по использованию (время работы, запущенные приложения).

Автоматическая проверка – фоновый процесс проверяет лимиты времени и при достижении блокирует доступ (в некоторых реализациях).

Цветной вывод – наглядные сообщения в терминале.

Кроссплатформенность – работает на Windows, Linux и macOS.

📖 Использование
Синтаксис (единый для всех версий):

bash
<команда> <субкоманда> [аргументы...] [опции]
Субкоманды
Команда	Описание
add site <domain>	Заблокировать сайт (домен)
add time <minutes>	Установить лимит времени (минуты)
add app <process_name>	Запретить запуск приложения (имя процесса)
remove site|time|app <value>	Удалить правило по значению
list	Показать все правила
enable	Активировать родительский контроль
disable	Деактивировать родительский контроль
status	Показать текущее состояние
report	Сгенерировать отчёт об активности
Примеры
bash
# Заблокировать сайт youtube.com
python parent_control.py add site youtube.com

# Установить лимит времени 60 минут
python parent_control.py add time 60

# Запретить запуск игры
python parent_control.py add app game.exe

# Посмотреть все правила
python parent_control.py list

# Включить контроль
python parent_control.py enable

# Получить отчёт
python parent_control.py report
🛠 Установка и запуск
Python
bash
python parent_control.py <subcommand> [args...]
Требуется Python 3.6+.

Go
bash
go build parent_control.go
./parent_control <subcommand> [args...]
JavaScript (Node.js)
bash
node parent_control.js <subcommand> [args...]
C++
bash
g++ -std=c++17 parent_control.cpp -o parent_control
./parent_control <subcommand> [args...]
C#
bash
csc parent_control.cs
mono parent_control.exe <subcommand> [args...]   # или dotnet run
Java
bash
javac parent_control.java
java parent_control <subcommand> [args...]
Ruby
bash
ruby parent_control.rb <subcommand> [args...]
🧠 Формат конфигурации
Данные хранятся в JSON-файле ~/.parent_control.json:

json
{
  "rules": {
    "sites": ["youtube.com", "facebook.com"],
    "time_limit": 120,
    "apps": ["game.exe", "steam.exe"]
  },
  "active": true,
  "start_time": "2025-06-28T10:00:00",
  "total_time": 0
}
rules – список правил.

active – включен ли контроль.

start_time – время начала текущей сессии (для учёта времени).

total_time – общее накопленное время работы за день.

✨ Дополнительные фичи
Сброс времени – автоматический сброс счётчика в полночь.

Уведомления – вывод сообщений при приближении к лимиту (в некоторых реализациях).

Защита от обхода – программа периодически перепроверяет hosts и процессы.

📂 Состав репозитория
Язык	Файл	Статус
Python	parent_control.py	✅
C++	parent_control.cpp	✅
Go	parent_control.go	✅
JavaScript	parent_control.js	✅
C#	parent_control.cs	✅
Ruby	parent_control.rb	✅
Java	parent_control.java	✅
🤝 Вклад в проект
Приветствуются улучшения:

Графический интерфейс.

Поддержка нескольких пользователей.

Интеграция с системными службами.

Создавайте Issues и Pull Requests.

📜 Лицензия
MIT License – свободное использование, модификация и распространение.

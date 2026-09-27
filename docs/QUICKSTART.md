# Запуск проектов

[← На главную](../README.md)

Клонируйте репозиторий и переходите в папку нужного проекта. Команды ниже предполагают, что необходимые инструменты доступны в PATH.

| Проект | Требования | Команды внутри папки |
|---|---|---|
| Дело | Node.js 24+ | `node server.mjs` |
| StockFlow | PHP 8.2+, pdo_mysql, MySQL/MariaDB | `php scripts/install.php --demo`, затем `php -S 127.0.0.1:8083 -t public public/router.php` |
| FocusDesk | Windows, .NET 10 SDK | `dotnet run --project src/FocusDesk.App -c Release` |
| Светолесье | Windows, .NET 10 SDK | `dotnet run --project src/Svetolesye.Game -c Release` |
| Сайт игры | Node.js 24+ | `node server.mjs` |
| CampusMate | Node.js 24, pnpm 11 | `pnpm install --frozen-lockfile`, затем `pnpm exec expo start --web --port 8081` |
| PocketBudget | Node.js 24, pnpm 11 | `pnpm install --frozen-lockfile`, затем `pnpm exec expo start --web --port 8082` |

| HikkiAnime | Node.js 24+ | `node server.mjs` (адрес выводится в терминале) |

## StockFlow и XAMPP

Включите MySQL. Если PHP не добавлен в PATH, в PowerShell замените `php` на `& C:\xampp\php\php.exe`.
Настройте `config.local.php` по образцу `config.example.php` или задайте переменные среды, перечисленные в [README](../stockflow/README.md). Установка добавляет схему, склады и, с флагом `--demo`, примеры товаров. Первого администратора создайте через интерфейс. Порт 8083 позволяет одновременно запускать мобильные проекты.

## iPhone и Expo Go

1. Выполните `pnpm install --frozen-lockfile` в папке мобильного проекта.
2. Проекты закреплены на Expo SDK 54: установленная версия Expo Go должна поддерживать этот SDK. Если не поддерживает, используйте web-версию или обновите SDK с зависимостями.
3. Подключите телефон и компьютер к одной Wi-Fi сети.
4. Запустите `node tools/launch-expo.mjs 8081` для CampusMate или `node tools/launch-expo.mjs 8082` для PocketBudget.
5. Отсканируйте QR-код камерой iPhone. Если потребуется вход, авторизуйтесь в своём аккаунте Expo.

Для разработки через Expo Go с Windows Mac не нужен. Отдельная устанавливаемая iOS-сборка требует подписания; в этом репозитории она не поставляется.

## Web-сборки мобильных приложений

```sh
pnpm export:web
node tools/serve.mjs 8091
```

Для второго приложения используйте 8092. Содержимое созданной `dist/` можно развернуть в корне отдельного статического HTTPS-сайта. PWA использует абсолютные пути `/`: размещение в подпапке требует изменения базовых путей. После первого открытия по HTTPS доступен offline-кэш. На локальном HTTP-адресе компьютера iPhone не включает service worker.

## Где хранятся записи

Дело — локальная SQLite; StockFlow — MySQL; FocusDesk — SQLite в `Data`; Светолесье — JSON в `Data`; мобильные приложения — AsyncStorage/хранилище браузера. У пользователей отдельные локальные данные. Git сохраняет исходный код, а для пользовательских записей нужны отдельные резервные копии.

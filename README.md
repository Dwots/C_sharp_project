# GameLib API

REST API для каталога видеоигр и личных библиотек пользователей: игры, категории, пользователи и их коллекции с оценками. Учебный проект на ASP.NET Core 8.

## Возможности

- **CRUD** по играм, категориям, пользователям и связке «пользователь ↔ игра»
- **Две схемы аутентификации**: JWT Bearer и API-ключ через заголовок `X-API-KEY`
- **Роли и авторизация**: `Admin`, `Manager`, `User` — разграничение по эндпоинтам
- **Пагинация, поиск, сортировка и фильтрация** игр (по цене, разработчику, категории)
- **Кэширование** в Redis
- **Идемпотентность** POST-запросов через заголовок `Idempotency-Key`
- **Rate limiting** — 100 запросов в минуту на клиента, ответ `429` при превышении
- **Health checks** — состояние API, PostgreSQL и Redis одним запросом
- **Метрики Prometheus** + готовые дашборды Grafana
- **Валидация** через FluentValidation, единая обработка ошибок в middleware
- **Swagger UI** с формами для JWT и API-ключа

## Стек

| Слой | Технология |
|---|---|
| Runtime | .NET 8 / ASP.NET Core |
| БД | PostgreSQL 16 |
| Доступ к данным | EF Core + Dapper (категории) |
| Миграции | Liquibase |
| Кэш | Redis |
| Аутентификация | JWT Bearer, BCrypt для паролей |
| Валидация | FluentValidation |
| Документация | Swagger / Swashbuckle |
| Мониторинг | prometheus-net, Prometheus, Grafana |
| Тесты | xUnit, Moq, FluentAssertions, EF InMemory |

## Быстрый старт через Docker

Поднимает всё сразу: API, БД с накатанными миграциями, Redis, Prometheus и Grafana.

```bash
docker compose up -d --build
```

| Сервис | Адрес | Примечание |
|---|---|---|
| API (Swagger) | http://localhost:5000/swagger | |
| Health check | http://localhost:5000/health | |
| Метрики | http://localhost:5000/metrics | |
| PostgreSQL | localhost:5434 | `gameuser` / `gamepass`, база `gamelibdb` |
| Redis | localhost:6379 | |
| Prometheus | http://localhost:9090 | |
| Grafana | http://localhost:3000 | `admin` / `admin` |

Остановить: `docker compose down` (с удалением данных — `docker compose down -v`).

## Локальный запуск

API ожидает PostgreSQL на порту `5434` и Redis на `6379`. Проще всего поднять только инфраструктуру в Docker, а само приложение запускать с хоста:

```bash
docker compose up -d postgres redis liquibase
dotnet run
```

Приложение слушает **http://localhost:5000** — порт задан секцией `Kestrel` в `appsettings.Development.json`.

## Учётные данные по умолчанию

Liquibase при первом запуске создаёт таблицы и наполняет их сидами:

- **Администратор** — `admin@gamelib.com` / `Admin123!`
- **API-ключ для разработки** — `dev-api-key-12345`

## Как обращаться к API

Все эндпоинты, кроме `/api/auth/login`, `/api/auth/register` и `/health`, требуют авторизации.

**1. Получить токен:**

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@gamelib.com","password":"Admin123!"}'
```

В ответе — `token`, `username`, `role` и `expiresAt`.

**2. Использовать токен:**

```bash
curl http://localhost:5000/api/game \
  -H "Authorization: Bearer <токен>"
```

**Либо вместо токена — API-ключ:**

```bash
curl http://localhost:5000/api/game -H "X-API-KEY: dev-api-key-12345"
```

## Эндпоинты

### Аутентификация

| Метод | Путь | Доступ |
|---|---|---|
| POST | `/api/auth/login` | Все |
| POST | `/api/auth/register` | Все |
| GET | `/api/auth/me` | Любой авторизованный |

### Игры

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/game` | Admin, Manager, User |
| GET | `/api/game/{id}` | Admin, Manager, User |
| GET | `/api/game/paged` | Admin, Manager, User |
| POST | `/api/game` | Admin, Manager |
| PUT | `/api/game/{id}` | Admin, Manager |
| DELETE | `/api/game/{id}` | Admin |

Параметры `/api/game/paged`: `page` (с 1), `pageSize` (до 100), `search`, `sortBy`, `sortDescending`, `minPrice`, `maxPrice`, `developer`, `categoryId`.

### Категории

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/category` | Admin, Manager, User |
| GET | `/api/category/{id}` | Admin, Manager, User |
| POST | `/api/category` | Admin, Manager |
| PUT | `/api/category/{id}` | Admin, Manager |
| DELETE | `/api/category/{id}` | Admin |

### Пользователи

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/user` | Admin, Manager |
| GET | `/api/user/{id}` | Admin, Manager, User |
| POST | `/api/user` | Admin |
| PUT | `/api/user/{id}` | Admin, Manager, User |
| DELETE | `/api/user/{id}` | Admin |

### Библиотека пользователя

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/users/{userId}/games` | Admin, Manager, User |
| POST | `/api/users/{userId}/games` | Admin, Manager, User |
| PUT | `/api/users/{userId}/games/{gameId}` | Admin, Manager, User |
| DELETE | `/api/users/{userId}/games/{gameId}` | Admin, Manager, User |

### Служебные

| Метод | Путь | Доступ |
|---|---|---|
| GET | `/health` | Все |
| GET | `/metrics` | Все |
| GET | `/swagger` | Все |

## Идемпотентность

POST-запрос с заголовком `Idempotency-Key` выполняется один раз: ответ кладётся в Redis, и повторный запрос с тем же ключом вернёт закэшированный результат вместо создания дубля.

```bash
curl -X POST http://localhost:5000/api/game \
  -H "Authorization: Bearer <токен>" \
  -H "Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

## Тесты

```bash
dotnet test
```

21 тест на репозитории игр, пользователей и категорий (EF Core InMemory).

## Структура проекта

```
Controllers/    — HTTP-эндпоинты
Services/       — бизнес-логика
Repositories/   — доступ к данным (EF Core + Dapper)
Models/         — сущности БД
DTOs/           — контракты запросов и ответов
Validators/     — правила FluentValidation
Middleware/     — API-ключи, идемпотентность, метрики, логирование, обработка ошибок
Data/           — DbContext
db/             — миграции Liquibase
monitoring/     — конфиги Prometheus и Grafana
tests/          — юнит-тесты
```

## Конфигурация

Локально настройки берутся из `appsettings.Development.json`, в Docker переопределяются переменными окружения в `compose.yaml`:

| Переменная | Назначение |
|---|---|
| `ConnectionStrings__DefaultConnection` | Строка подключения к PostgreSQL |
| `ConnectionStrings__Redis` | Адрес Redis |
| `AuthSettings__SecretKey` | Ключ подписи JWT, минимум 32 символа |

> Ключ подписи JWT и пароли БД лежат в репозитории в открытом виде — это допустимо для учебного проекта, но для реального развёртывания их нужно вынести в секреты.

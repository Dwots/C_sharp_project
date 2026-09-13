# GameLib API

REST API для каталога видеоигр и личных библиотек пользователей: игры, категории, пользователи и их коллекции с оценками. Учебный проект на ASP.NET Core 8.

> **Отношение к репозиторию `C_sharp_Baranov`**
>
> Сам сервис здесь тот же самый. Отличие в том, что в этом репозитории вдобавок
> выполнены **лабораторные работы по базам данных**: индексы и `EXPLAIN ANALYZE`,
> поведение при росте данных, партиционирование с автоматизацией и алертингом.
>
> Под них в проект добавлены растущая сущность `play_sessions`, миграции с
> индексами и партициями, фоновая служба обслуживания партиций и уведомления
> в Telegram. Отчёты лежат в [`docs/`](docs/).

## Как обновлять репозитории

Проект живёт внутри общего репозитория [`Dwots/VSCode`](https://github.com/Dwots/VSCode)
в папке `C#/C_sharp_project`, а на GitHub выложен ещё и отдельно. Локальный
репозиторий при этом **один** — `.git` лежит только в корне `VSCode`,
внутри папки проекта его нет. Различаются лишь адреса отправки:

```bash
# всё целиком -> Dwots/VSCode
git push origin main

# только папку проекта -> Dwots/C_sharp_project
git subtree push --prefix="C#/C_sharp_project" csharp main
```

Обе команды запускаются из любого места внутри `VSCode`: git сам поднимается
до корня репозитория. По той же причине `git push` из папки проекта отправит
**весь** репозиторий, а не только её.

Ключевая деталь — `--prefix`. Без него `git push csharp main` отправил бы в
репозиторий проекта всё содержимое `VSCode`, включая посторонние папки.

Если remote `csharp` ещё не настроен:

```bash
git remote add csharp https://github.com/Dwots/C_sharp_project.git
```

## Лабораторные работы по базам данных

| Работа | Отчёт | Что добавлено в проект |
|---|---|---|
| №1. Индексы и EXPLAIN ANALYZE | [`docs/lab-01-indexes.md`](docs/lab-01-indexes.md) | таблица `play_sessions`, индексы миграцией `008` |
| №2. Когда индексов недостаточно | [`docs/lab-02-growth.md`](docs/lab-02-growth.md) | замеры на объёмах до 10 млн строк, покрывающий индекс |
| №3. Партиционирование | [`docs/lab-03-partitioning.md`](docs/lab-03-partitioning.md) | миграция `009`, `PartitionService`, job, health check, алерты |

Вспомогательные SQL-скрипты и сценарий демонстрации — в [`docs/lab-01/`](docs/lab-01/), [`docs/lab-02/`](docs/lab-02/), [`docs/lab-03/`](docs/lab-03/).

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
| pgAdmin | http://localhost:5050 | `admin@gamelib.com` / `admin`, пароль к БД `gamepass` |

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

### Игровые сессии

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/users/{userId}/sessions` | Admin, Manager, User |
| POST | `/api/users/{userId}/sessions` | Admin, Manager, User |
| GET | `/api/users/{userId}/activity` | Admin, Manager, User |

Параметры `/api/users/{userId}/sessions`: `page` (с 1), `pageSize` (до 100), `platform`, `from`, `to`, `minDuration`. Сортировка — всегда по `created_at DESC`.

Обычный пользователь видит только свои сессии; Admin и Manager — любые.

### Статистика

| Метод | Путь | Роли |
|---|---|---|
| GET | `/api/stats/top-games` | Admin, Manager, User |

Параметры: `limit` (1–100, по умолчанию 10), `days` — учитывать только последние N дней.

### Служебные

| Метод | Путь | Доступ |
|---|---|---|
| GET | `/health` | Все |
| GET | `/metrics` | Все |
| GET | `/swagger` | Все |

## Схема БД

```
                  ┌──────────────┐         ┌────────────────┐         ┌──────────────┐
                  │    users     │         │ game_categories│         │  categories  │
                  ├──────────────┤         ├────────────────┤         ├──────────────┤
                  │ id       PK  │         │ game_id     FK │────────>│ id       PK  │
                  │ username  UQ │         │ category_id FK │         │ name         │
                  │ email     UQ │         └────────────────┘         │ description  │
                  │ password_hash│                  │                 └──────────────┘
                  │ role         │                  │
                  │ created_at   │                  v
                  └──────────────┘         ┌──────────────┐
                     │        │            │    games     │
                     │        │            ├──────────────┤
                     │        │            │ id       PK  │
                     │        │            │ title        │
                     │        │            │ price        │
                     │        │            │ developer    │
                     │        │            │ release_date │
                     │        │            │ created_at   │
                     │        │            └──────────────┘
                     │        │               │        │
                     v        │               │        │
            ┌────────────────┐│               │        │
            │   user_games   ││<──────────────┘        │
            ├────────────────┤│                        │
            │ user_id  PK FK ││                        │
            │ game_id  PK FK ││                        │
            │ rating         ││                        │
            │ added_at       ││                        │
            └────────────────┘│                        │
                              v                        v
                     ┌──────────────────────────────────────┐
                     │           play_sessions              │  ← растущая
                     ├──────────────────────────────────────┤
                     │ id               PK  BIGSERIAL       │
                     │ user_id          FK  -> users(id)    │
                     │ game_id          FK  -> games(id)    │
                     │ duration_minutes                     │
                     │ platform                             │
                     │ created_at                           │
                     └──────────────────────────────────────┘

            ┌──────────────┐
            │   api_keys   │  (техническая таблица, связей не имеет)
            ├──────────────┤
            │ id       PK  │
            │ key_hash  UQ │
            │ name, role   │
            │ is_active    │
            │ expires_at   │
            │ created_at   │
            └──────────────┘
```

**Связи:**

| Тип | Где |
|---|---|
| one-to-many | `users` → `play_sessions`, `games` → `play_sessions` |
| many-to-many | `games` ↔ `categories` через `game_categories` |
| many-to-many | `users` ↔ `games` через `user_games` (с атрибутом `rating`) |

Все таблицы создаются миграциями Liquibase из `db/changelogs/`, вручную схема не правится.

---

## Основная сущность для масштабирования

```
Основная сущность для масштабирования:
play_sessions
```

**Почему она подходит:**

`play_sessions` — журнал фактов «пользователь играл в игру столько-то минут». В отличие от остальных таблиц, её рост ничем не ограничен сверху:

- `users`, `games`, `categories` растут медленно и линейно — это справочники;
- `user_games` ограничена сверху произведением «пользователи × игры»: одна пара может встретиться в ней лишь однажды, потому что `PRIMARY KEY (user_id, game_id)`;
- `play_sessions` копится **каждый раз, когда кто-то запускает игру**. Одна и та же пара «пользователь + игра» порождает новую строку хоть по нескольку раз в день.

При 10 000 активных пользователей и 2 сессиях в день это ~7,3 млн строк в год; при 100 000 пользователей — свыше 70 млн.

Отдельно важно, что таблица пригодна для будущего партиционирования по времени: у неё есть суррогатный `id BIGSERIAL` и колонка `created_at`, а ключ партиционирования в PostgreSQL обязан входить в первичный ключ. У `user_games` с составным ключом `(user_id, game_id)` такой возможности нет — это и было причиной завести отдельную таблицу.

**Поля, по которым чаще всего идут поиск и фильтрация:** `user_id`, `created_at`, `platform`, `game_id`.

---

## Сложные запросы

Все перечисленные запросы реально выполняются сервисом — они лежат в `Repositories/PlaySessionRepository.cs` явным SQL (Dapper), а не прячутся за LINQ.

### JOIN-запрос №1 — история сессий пользователя

Три таблицы. Обслуживает `GET /api/users/{userId}/sessions`.

```sql
SELECT
    ps.id, ps.user_id, u.username,
    ps.game_id, g.title AS game_title,
    ps.duration_minutes, ps.platform, ps.created_at
FROM play_sessions ps
JOIN users u ON u.id = ps.user_id
JOIN games g ON g.id = ps.game_id
WHERE ps.user_id = @UserId
  AND ps.platform = @Platform
  AND ps.created_at >= @From
ORDER BY ps.created_at DESC
LIMIT @Limit OFFSET @Offset;
```

### JOIN-запрос №2 — топ игр по наигранному времени

Четыре таблицы плюс CTE. Обслуживает `GET /api/stats/top-games`.

Сессии агрегируются **до** присоединения жанров: если джойнить `game_categories` сразу, `LIMIT` применится к парам «игра × жанр» и одна и та же игра займёт несколько строк выдачи.

```sql
WITH game_totals AS (
    SELECT
        ps.game_id,
        COUNT(*)                   AS sessions_count,
        COUNT(DISTINCT ps.user_id) AS unique_players,
        SUM(ps.duration_minutes)   AS total_minutes,
        ROUND(AVG(ps.duration_minutes), 1)::float8 AS avg_minutes
    FROM play_sessions ps
    WHERE (@From::timestamp IS NULL OR ps.created_at >= @From::timestamp)
    GROUP BY ps.game_id
    ORDER BY total_minutes DESC
    LIMIT @Limit
)
SELECT
    g.id, g.title,
    string_agg(DISTINCT c.name, ', ') AS categories,
    t.sessions_count, t.unique_players, t.total_minutes, t.avg_minutes
FROM game_totals t
JOIN games g                 ON g.id = t.game_id
LEFT JOIN game_categories gc ON gc.game_id = g.id
LEFT JOIN categories c       ON c.id = gc.category_id
GROUP BY g.id, g.title, t.sessions_count, t.unique_players,
         t.total_minutes, t.avg_minutes
ORDER BY t.total_minutes DESC;
```

### Агрегирующий запрос — активность пользователя

`COUNT`, `COUNT(DISTINCT)`, `SUM`, `MAX` и `GROUP BY`. Обслуживает `GET /api/users/{userId}/activity`.

```sql
SELECT
    u.id, u.username,
    COUNT(ps.id)                          AS sessions_count,
    COUNT(DISTINCT ps.game_id)            AS distinct_games,
    COALESCE(SUM(ps.duration_minutes), 0) AS total_minutes,
    MAX(ps.created_at)                    AS last_played_at
FROM users u
LEFT JOIN play_sessions ps ON ps.user_id = u.id
WHERE u.id = @UserId
GROUP BY u.id, u.username;
```

`LEFT JOIN` здесь принципиален: пользователь без единой сессии всё равно должен вернуться — с нулями, а не с пустым ответом.

---

## Генерация данных

Миграции создают только минимальный набор для проверки API: администратора, 10 жанров и dev-ключ. Большие объёмы заливаются отдельным скриптом — в миграциях им не место, иначе миллион строк уезжал бы в базу при каждом старте.

```bash
# по умолчанию: 10 000 пользователей, 1 000 игр, 1 000 000 сессий
./db/seed/generate.sh

# свои объёмы: пользователи, игры, сессии
./db/seed/generate.sh 100000 5000 5000000
```

Либо напрямую, без Docker:

```bash
psql -h localhost -p 5434 -U gameuser -d gamelibdb \
     -v users=10000 -v games=1000 -v sessions=1000000 \
     -f db/seed/generate_data.sql
```

Скрипт заполняет `users`, `games`, `game_categories`, `user_games` и `play_sessions`, после чего выполняет `ANALYZE` по всем таблицам — без свежей статистики планировщик ошибается в оценках и замеры `EXPLAIN ANALYZE` становятся недостоверными.

Ориентир по времени на объёме по умолчанию: около минуты, из них ~35 секунд — вставка миллиона сессий. Пароль у всех сгенерированных пользователей тот же, что у админа — `Admin123!`.

---

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

## Архитектура

```
Client
  │
  v
Controller   — разбор HTTP, проверка ролей. К БД не обращается.
  │
  v
Service      — бизнес-логика, права доступа, кэш, валидация связей
  │
  v
Repository   — единственное место, где выполняются SQL-запросы
  │
  v
PostgreSQL
```

Контроллеры не работают с базой напрямую: они знают только про сервисы, а те — только про интерфейсы репозиториев (`Repositories/Interfaces/`). Подключение к PostgreSQL создаётся в двух местах и больше нигде:

- `Data/AppDbContext.cs` — EF Core, строка подключения приходит из DI (`Program.cs`);
- репозитории на Dapper (`CategoryDapperRepository`, `PlaySessionRepository`) — `NpgsqlConnection` создаётся в методе `CreateConnection()` из `ConnectionStrings:DefaultConnection`.

Запросы к `play_sessions` намеренно написаны на Dapper явным SQL: это основная растущая таблица, и её запросы должны быть видимы и пригодны для `EXPLAIN ANALYZE` без разглядывания сгенерированного LINQ.

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
db/changelogs/  — миграции Liquibase
db/seed/        — генератор тестовых данных
docs/           — отчёты по лабораторным работам
monitoring/     — конфиги Prometheus, Grafana и pgAdmin
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

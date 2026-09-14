# Лабораторная работа №4 — Масштабирование чтения PostgreSQL: Primary + Replica

**Проект:** GameLib API (ASP.NET Core 8 + PostgreSQL 16)
**Дата выполнения:** 14.09.2026
**Статус:** выполнено полностью, части 1–6 и контрольные вопросы

---

## Часть 1. Primary и Replica

### Фрагмент compose.yaml

```yaml
  postgres:
    image: postgres:16-alpine
    container_name: gamelib_db
    environment:
      POSTGRES_USER: gameuser
      POSTGRES_PASSWORD: gamepass
      POSTGRES_DB: gamelibdb
    ports:
      - "5434:5432"
    command:
      - postgres
      - -c
      - hba_file=/etc/postgresql/pg_hba.conf
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./db/replication/pg_hba.conf:/etc/postgresql/pg_hba.conf:ro

  postgres_replica:
    image: postgres:16-alpine
    container_name: gamelib_db_replica
    user: postgres
    environment:
      PGPASSWORD: replpass
      PGDATA: /var/lib/postgresql/data
    ports:
      - "5435:5432"
    volumes:
      - postgres_replica_data:/var/lib/postgresql/data
    depends_on:
      postgres:
        condition: service_healthy
    command: >
      sh -c '
      if [ ! -s "$$PGDATA/PG_VERSION" ]; then
        rm -rf "$$PGDATA"/*;
        until pg_basebackup -h postgres -p 5432 -U replicator -D "$$PGDATA" -Fp -Xs -R -P; do
          sleep 3;
        done;
        chmod 0700 "$$PGDATA";
      fi;
      exec postgres -c hot_standby=on
      '
```

### Какой контейнер чем является

| Роль | Контейнер | Порт с хоста | Адрес в сети Docker |
|---|---|---|---|
| Primary | `gamelib_db` | 5434 | `postgres:5432` |
| Replica | `gamelib_db_replica` | 5435 | `postgres_replica:5432` |

```sql
SELECT pg_is_in_recovery();
```

| Сервер | Результат |
|---|---|
| Primary | `f` |
| Replica | `t` |

> **Подтверждает:** сервер сам сообщает свою роль. `t` означает режим постоянного проигрывания журнала, то есть перед нами реплика, а не отдельная база.

### Как подключиться

```bash
# Primary
docker exec -it gamelib_db psql -U gameuser -d gamelibdb
psql -h localhost -p 5434 -U gameuser -d gamelibdb

# Replica
docker exec -it gamelib_db_replica psql -U gameuser -d gamelibdb
psql -h localhost -p 5435 -U gameuser -d gamelibdb
```

### Оба экземпляра запущены

```
NAMES                STATUS
gamelib_db           Up (healthy)
gamelib_db_replica   Up
```

> **Подтверждает:** оба экземпляра PostgreSQL запущены и работают одновременно.

---

## Часть 2. Streaming replication

### Цепочка передачи изменений

```
Primary изменяет данные
        ↓
изменение фиксируется в WAL
        ↓
Replica получает поток WAL
        ↓
Replica воспроизводит изменения у себя
```

### Что настроено

**`wal_level`** — подошло значение по умолчанию:

```
   name    | setting | boot_val | source  |         enumvals
-----------+---------+----------+---------+---------------------------
 wal_level | replica | replica  | default | {minimal,replica,logical}
```

> **Подтверждает:** `source = default` — значение встроенное, подходит для репликации без изменений. Менять параметр не потребовалось.

**Правила доступа** — [`db/replication/pg_hba.conf`](../db/replication/pg_hba.conf), добавлена строка:

```
host    replication     replicator      all                     scram-sha-256
```

```sql
SELECT type, database, user_name, address, auth_method
FROM pg_hba_file_rules WHERE 'replication' = ANY(database);
```

```
 type  |   database    |  user_name   |  address  |  auth_method
-------+---------------+--------------+-----------+---------------
 local | {replication} | {all}        |           | trust
 host  | {replication} | {all}        | 127.0.0.1 | trust
 host  | {replication} | {all}        | ::1       | trust
 host  | {replication} | {replicator} | all       | scram-sha-256
```

> **Подтверждает:** последняя строка — добавленная. Сервер её разобрал и принял, значит подключение по протоколу репликации из сети Docker разрешено. Первые три — стандартные, только для localhost, их бы не хватило.

**Роль репликации** — миграция [`db/changelogs/010_replication_role.sql`](../db/changelogs/010_replication_role.sql):

```sql
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replpass';
```

```
  rolname   | rolreplication | rolcanlogin | rolsuper
------------+----------------+-------------+----------
 replicator | t              | t           | f
```

> **Подтверждает:** роль существует, умеет подключаться (`rolcanlogin = t`) и получать поток WAL (`rolreplication = t`). Прав суперпользователя нет.

### Запуск Replica

```
2884357/2884357 kB (100%), 1/1 tablespace
LOG:  entering standby mode
LOG:  redo starts at 2/6F000028
LOG:  consistent recovery state reached at 2/6F000138
LOG:  database system is ready to accept read-only connections
LOG:  started streaming WAL from primary at 2/70000000 on timeline 1
```

> **Подтверждает:** три ключевые строки. `entering standby mode` — сервер опознал себя репликой; `ready to accept read-only connections` — принимает только чтение; `started streaming WAL from primary` — поток журнала с Primary установлен.

### Состояние репликации на Primary

```sql
SELECT * FROM pg_stat_replication;
```

```
 client_addr |  usename   |   state   | sync_state |  sent_lsn  | write_lsn  | flush_lsn  | replay_lsn
-------------+------------+-----------+------------+------------+------------+------------+------------
 10.200.5.8  | replicator | streaming | async      | 2/70000148 | 2/70000148 | 2/70000148 | 2/70000148
```

> **Подтверждает:** наличие строки означает, что к Primary подключена реплика. `state = streaming` — штатный режим передачи. Все четыре LSN совпадают, то есть отправленное по журналу уже применено — отставания нет.

---

## Часть 3. Доказательство работы репликации

### Операция записи на Primary

```sql
INSERT INTO games (title, description, price, developer, release_date)
VALUES ('Проверка репликации', 'лабораторная 4', 777, 'Replication Test', '2026-09-14')
RETURNING id, title, price;
```

```
  id  |        title        | price
------+---------------------+--------
 1001 | Проверка репликации | 777.00
INSERT 0 1
```

> **Подтверждает:** запись выполнена на Primary, присвоен `id = 1001`.

### Результат SELECT на Replica

```sql
SELECT id, title, price FROM games WHERE title = 'Проверка репликации';
```

```
  id  |        title        | price
------+---------------------+--------
 1001 | Проверка репликации | 777.00
```

> **Подтверждает:** строка, вставленная **только** в Primary, найдена на Replica — с тем же `id` и теми же значениями. Никаких команд на реплике для этого не выполнялось: данные приехали сами, потоком WAL. Репликация работает.

### Подтверждение подключённой Replica

```
 client_addr |  usename   |   state   | sync_state
-------------+------------+-----------+------------
 10.200.5.8  | replicator | streaming | async
```

> **Подтверждает:** канал, по которому строка попала на реплику, — вот он. `streaming` означает, что поток активен в момент проверки.

---

## Часть 4. Read-only поведение Replica

### Результат

```sql
INSERT INTO games (title, price) VALUES ('Попытка записи на реплику', 1);
```
```
ERROR:  cannot execute INSERT in a read-only transaction
```

```sql
UPDATE games SET price = 0 WHERE id = 1001;
```
```
ERROR:  cannot execute UPDATE in a read-only transaction
```

> **Подтверждает:** обе операции записи отклонены самим сервером, а не приложением. Реплика доступна только для чтения — это её штатное поведение, а не сбой.

### Почему Replica не должна использоваться приложением как независимая база для записи

Запрет здесь не мера предосторожности, а физическая необходимость.

Файлы данных реплики — результат побайтового воспроизведения журнала Primary. Собственная запись сделала бы их отличными от оригинала, и следующая же запись из потока легла бы поверх — на страницу, которая уже не совпадает с исходной. Состояние разошлось бы, и реплика перестала быть копией.

Отсюда следствие для приложения: реплика годится только для чтения, любая операция записи обязана идти на Primary. Приложение должно держать два подключения и осознанно выбирать между ними, а не рассматривать реплику как запасную базу.

---

## Часть 5. Чтение собственного сервиса через Replica

Выбранный сценарий: **`GET /api/stats/top-games`** — агрегат по игровым сессиям (`COUNT`, `COUNT DISTINCT`, `SUM` по сотням тысяч строк). Запрос тяжёлый, аналитический и не требует последних миллисекунд данных.

### Конфигурация: отдельное подключение к Replica

`compose.yaml`, сервис `api`:

```yaml
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=gamelibdb;Username=gameuser;Password=gamepass
      - ConnectionStrings__ReplicaConnection=Host=postgres_replica;Port=5432;Database=gamelibdb;Username=gameuser;Password=gamepass
```

### Код: два подключения

`Repositories/PlaySessionRepository.cs`:

```csharp
_connectionString = configuration.GetConnectionString("DefaultConnection")!;

// Если реплика не настроена, читаем с Primary: проект должен подниматься и без неё.
_replicaConnectionString =
    configuration.GetConnectionString("ReplicaConnection") ?? _connectionString;

/// <summary>Подключение к Primary. Все операции записи идут только сюда.</summary>
private NpgsqlConnection CreateConnection() => new(_connectionString);

/// <summary>Подключение к Replica — только для чтения.</summary>
private NpgsqlConnection CreateReplicaConnection() => new(_replicaConnectionString);
```

### Код: выбранный SELECT идёт на Replica

Метод `GetTopGamesAsync`:

```csharp
// Читаем с Replica, а не с Primary: запрос аналитический, тяжёлый
// и не требует последних миллисекунд данных.
await using var connection = CreateReplicaConnection();
await connection.OpenAsync();

var result = (await connection.QueryAsync<GameStatsDto>(sql, new { From = from, Limit = limit })).ToList();

// Показывает, с какого сервера пришёл ответ: на Replica вернёт true.
var isReplica = await connection.ExecuteScalarAsync<bool>("SELECT pg_is_in_recovery()");
_logger.LogInformation(
    "[Dapper] Top games: {Count} rows | источник: {Source}",
    result.Count, isReplica ? "Replica" : "Primary");
```

Остальные методы репозитория — создание сессий, история пользователя, активность — используют `CreateConnection()`, то есть по-прежнему работают с Primary.

### Проверка

```bash
curl "http://localhost:5000/api/stats/top-games?limit=5&days=30" -H 'X-API-KEY: dev-api-key-12345'
docker logs gamelib_api | grep 'Top games'
```

```
HTTP 200
[Dapper] Top games: 5 rows | источник: Replica
```

> **Подтверждает:** источник определён не по конфигурации, а самим сервером — запросом `pg_is_in_recovery()` внутри того же соединения, через которое пришли данные. Значение `Replica` означает, что ответ получен со standby-сервера, а не с Primary.

## Часть 6. Replication lag

### Попытка застать расхождение

```sql
-- Primary
INSERT INTO games (title, price, developer) VALUES ('Lag test 1', 1, 'lag');
-- Replica, сразу следом
SELECT count(*) FROM games WHERE title = 'Lag test 1';
```

```
 найдено_на_реплике
--------------------
                  1
```

Изменение уже применилось. Причина в масштабе времени: запуск `docker exec` занимает около 150 мс, а отставание — единицы миллисекунд.

### Измерение отставания

Чтобы увидеть величину, замер выполнен в одной сессии сразу после вставки:

```sql
INSERT INTO games (title, price, developer) VALUES ('Lag test 2', 1, 'lag');
SELECT pg_current_wal_lsn() AS primary_lsn,
       replay_lsn AS replica_replay,
       pg_current_wal_lsn() - replay_lsn AS отставание_байт,
       write_lag, flush_lag, replay_lag
FROM pg_stat_replication;
```

```
 primary_lsn | replica_replay | отставание_байт |    write_lag    |    flush_lag    |   replay_lag
-------------+----------------+-----------------+-----------------+-----------------+-----------------
 2/70005F00  | 2/70005F00     |               0 | 00:00:00.000273 | 00:00:00.001907 | 00:00:00.001922
```

> **Подтверждает:** отставание существует и измеримо. `replay_lag = 1.9 мс` — столько проходит от записи на Primary до момента, когда данные становятся видны запросам на Replica. Это в восемьдесят раз меньше времени запуска команды в консоли, поэтому «руками» устаревшее чтение не поймать.

### Момент расхождения под нагрузкой

Вставка 300 000 строк и замер сразу после неё:

```
 primary_lsn | replica_replay | отставание | replay_lag
-------------+----------------+------------+------------
 2/73D0E000  | 2/70005F28     | 61 MB      |
```

Замеры во время длительной вставки 3 млн строк:

```
замер 1 | 0 bytes    | —
замер 2 | 0 bytes    | —
замер 3 | 0 bytes    | —
замер 4 | 0 bytes    | —
замер 5 | 0 bytes    | —
замер 6 | 1312 kB    | 00:00:00.005185
```

> **Подтверждает:** момент расхождения зафиксирован. При потоке записи Primary ушёл вперёд на **61 МБ** журнала — это данные, которых на Replica в тот момент ещё не было. В шестом замере видно и то же самое во времени: `replay_lag = 5.2 мс`. Нулевые замеры объясняются тем, что большая вставка идёт одной транзакцией и WAL сбрасывается неравномерно.

### Главный вывод

Репликация не означает мгновенной синхронизации. Между записью на Primary и применением изменения на Replica существует задержка — **replication lag**.

В покое она составила 1.9 мс, под нагрузкой на запись выросла до 61 МБ неприменённого журнала. Величина не фиксирована: она зависит от интенсивности записи, скорости сети и того, успевает ли Replica проигрывать поток.

Практическое следствие для приложения: `SELECT`, отправленный на Replica сразу после `INSERT` на Primary, **может не увидеть только что записанные данные**. Поэтому на Replica направляют запросы, которым допустима слегка устаревшая картина, — как выбранный в части 5 агрегат статистики, — и не направляют те, где нужна гарантия чтения собственной записи.

---

# Контрольные вопросы

| № | Вопрос | Ответ |
|---|---|---|
| 1 | Чем Primary отличается от Replica? | Primary принимает запись и порождает WAL. Replica получает этот журнал и воспроизводит его у себя, принимая только чтение. Различить программно: `pg_is_in_recovery()` возвращает `f` на Primary и `t` на Replica. |
| 2 | Почему запись выполняем на Primary? | Потому что Replica физически не может её принять: её файлы данных — результат побайтового воспроизведения чужого журнала, и собственная запись разошлась бы со следующей записью из потока. Попытка даёт `ERROR: cannot execute INSERT in a read-only transaction`. |
| 3 | Как изменение из Primary попадает на Replica? | Primary изменяет данные → изменение фиксируется в WAL → Replica получает поток WAL по сети → Replica воспроизводит изменения у себя. Соединение видно на Primary в `pg_stat_replication` со статусом `streaming`. |
| 4 | Что такое WAL в контексте репликации? | Write-Ahead Log — журнал предзаписи, куда PostgreSQL пишет операции над страницами до изменения самих файлов данных. Изначально нужен для восстановления после сбоя; репликация использует тот же поток как канал передачи изменений. Важно, что WAL содержит **только изменения**, а не саму базу, — поэтому Replica сначала снимает полную копию через `pg_basebackup`, и лишь затем журнал её догоняет. |
| 5 | Что такое replication lag? | Задержка между моментом записи на Primary и моментом, когда изменение применено на Replica. Измеряется в байтах журнала (`pg_current_wal_lsn() - replay_lsn`) или во времени (`replay_lag`). Измерено: 1.9 мс в покое, до 61 МБ неприменённого журнала под нагрузкой. |
| 6 | Почему следующий SELECT после INSERT потенциально может увидеть старые данные, если его отправить на Replica? | Из-за асинхронной репликации (`sync_state = async`): Primary подтверждает транзакцию клиенту, **не дожидаясь** ответа Replica. В промежутке между подтверждением и применением журнала на Replica запрос к ней вернёт состояние без новой записи. При синхронной репликации этого бы не было, но каждая запись стала бы медленнее на круг по сети. |
| 7 | Что именно масштабируется при Read Scaling: скорость одного SQL-запроса или способность системы обслуживать больше чтений? | Способность обслуживать больше чтений. Один и тот же запрос на Replica выполняется примерно за то же время, что и на Primary, — оборудование и планы одинаковы. Выигрыш в том, что читающая нагрузка уходит с Primary, освобождая его ресурсы для записи, и что реплик можно добавить несколько. |
| 8 | Почему наличие Replica не отменяет необходимость индексов и оптимизации SQL? | Потому что Replica — точная копия Primary, включая схему, индексы и статистику. Запрос с `Seq Scan` по миллиону строк останется таким же на Replica: неэффективный план просто исполняется на другом сервере. Репликация распределяет нагрузку, но не ускоряет отдельный запрос. Более того, тяжёлые запросы на Replica конкурируют с проигрыванием WAL и могут увеличить lag. |
| 9 | Расскажите про CAP-теорему | Утверждение о том, что распределённая система не может одновременно обеспечивать все три свойства: **Consistency** — любое чтение возвращает последнюю запись; **Availability** — каждый запрос получает ответ; **Partition tolerance** — система работает при потере связи между узлами. Поскольку сетевые разделения в реальной системе неизбежны, выбор фактически идёт между C и A.<br><br>Настроенная конфигурация — наглядный пример этого выбора. Асинхронная репликация жертвует строгой согласованностью ради доступности и скорости: Primary подтверждает запись, не дожидаясь Replica, и чтение с Replica может вернуть устаревшие данные. Это модель **eventual consistency** — согласованность достигается, но не мгновенно.<br><br>Переключение на синхронную репликацию сместило бы баланс в сторону C: данные на Replica гарантированно актуальны, но при её недоступности Primary заблокирует запись, то есть потеряется A. |

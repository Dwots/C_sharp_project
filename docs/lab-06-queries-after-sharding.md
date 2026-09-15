# Лабораторная работа №6 — Запросы и архитектура после шардирования

**Проект:** GameLib API (ASP.NET Core 8 + PostgreSQL 16)
**Дата выполнения:** 15.09.2026
**Исходное состояние:** три шарда из работы №5 подняты, 100 000 сессий распределены по `user_id`

---

## Часть 1. Архитектура после шардирования

```
                        Backend (GameLib API)
                                │
                         IShardRouter
                     ┌──────────┼──────────┐
                     ▼          ▼          ▼
                  Shard 0    Shard 1    Shard 2
                  :5441      :5442      :5443
```

Router — интерфейс `IShardRouter` с двумя реализациями: `ModuloShardRouter` (`hash(user_id) % 3`) и `ConsistentHashRouter` (кольцо из 300 виртуальных точек). Обе получают на вход shard key и возвращают номер шарда.

Ключ шардирования — **`user_id`**, выбран в работе №5.

Принципиальное ограничение схемы: на шардах лежит **только** `play_sessions`. Справочники `users`, `games`, `categories`, `game_categories` остались в основной базе и на шарды не копировались — это видно в `db/sharding/init-shard.sql`, где создаётся одна таблица и один индекс. Последствия разбираются в части 4.

---

## Часть 2. Single-Shard Query

### Выбранный запрос

`GET /api/users/{userId}/sessions` — история сессий пользователя, `PlaySessionRepository.GetByUserAsync`:

```sql
SELECT ps.id, ps.user_id, ps.game_id, ps.duration_minutes, ps.platform, ps.created_at
FROM play_sessions ps
WHERE ps.user_id = @UserId
ORDER BY ps.created_at DESC
LIMIT @Limit OFFSET @Offset;
```

Условие `WHERE ps.user_id = @UserId` содержит shard key в виде равенства — это и делает запрос single-shard.

### Проверка: router называет шард

```bash
curl -s -H "X-Api-Key: dev-api-key-12345" \
  "http://localhost:5000/api/sharding/route/42?strategy=modulo"
```

```json
{"userId":42,"strategy":"hash(key) % 3","shardCount":3,"shard":2}
```

### Проверка: данные лежат именно там

```bash
for s in shard0 shard1 shard2; do
  docker compose exec -T $s psql -U gameuser -d shard \
    -c "SELECT count(*) FROM play_sessions WHERE user_id = 42;"
done
```

```
=== shard0 ===      === shard1 ===      === shard2 ===
 rows_user_42        rows_user_42        rows_user_42
--------------      --------------      --------------
            0                   0                  12
```

Вывод подтверждает: все 12 сессий пользователя 42 находятся на shard2, на shard0 и shard1 — ноль строк. Номер шарда, названный router, совпал с тем, где данные реально лежат.

### План запроса на целевом шарде

```bash
docker compose exec -T shard2 psql -U gameuser -d shard -c \
"EXPLAIN ANALYZE SELECT id, game_id, duration_minutes, platform, created_at
 FROM play_sessions WHERE user_id = 42 ORDER BY created_at DESC LIMIT 20;"
```

```
 Limit  (cost=38.92..38.94 rows=10 width=30) (actual time=3.285..3.290 rows=12 loops=1)
   ->  Sort  (cost=38.92..38.94 rows=10 width=30) (actual time=3.282..3.284 rows=12 loops=1)
         Sort Key: created_at DESC
         Sort Method: quicksort  Memory: 25kB
         ->  Bitmap Heap Scan on play_sessions  (cost=4.37..38.75 rows=10 width=30) (actual time=0.622..3.217 rows=12 loops=1)
               Recheck Cond: (user_id = 42)
               Heap Blocks: exact=12
               ->  Bitmap Index Scan on idx_shard_user_created  (cost=0.00..4.37 rows=10 width=0) (actual time=0.022..0.023 rows=12 loops=1)
                     Index Cond: (user_id = 42)
 Planning Time: 1.420 ms
 Execution Time: 3.419 ms
```

Что подтверждает вывод: `Index Cond: (user_id = 42)`, `rows=12`, `Execution Time: 3.419 ms` — шард отдаёт результат по индексу за миллисекунды, никаких обращений к соседям в плане нет.

### Почему запросу не нужны другие шарды

Router вычисляет номер шарда **из того же значения**, по которому запись туда попала. При записи `hash(42) % 3` дал 2 — строка ушла на shard2. При чтении та же функция от того же аргумента даёт то же самое 2. Функция детерминированная, значит адрес вычисляется однозначно.

Из этого следует, что строк пользователя 42 на других шардах не может быть **в принципе**: чтобы туда попасть, запись должна была получить другой номер от той же функции с тем же входом. Опрашивать shard0 и shard1 не нужно не потому, что там «скорее всего пусто», а потому, что там гарантированно пусто — вывод `0 / 0 / 12` это и показывает.

Формально: single-shard query — запрос, для которого набор затронутых шардов вычислим до выполнения. Здесь этот набор состоит из одного элемента.

-- ===========================================================================
-- Лабораторная работа №2 — запросы к заданиям 4-11.
-- В pgAdmin: выделить один запрос и нажать F5.
-- ===========================================================================


-- ЗАДАНИЕ 4. SELECT без дополнительного индекса --------------------------
-- Повторить на каждой контрольной точке: 10k, 100k, 1M, 5M.
EXPLAIN ANALYZE SELECT * FROM events WHERE user_id = 123;


-- ЗАДАНИЕ 5. Добавление индекса ------------------------------------------
CREATE INDEX idx_events_user_id ON events(user_id);
ANALYZE events;

EXPLAIN ANALYZE SELECT * FROM events WHERE user_id = 123;


-- ЗАДАНИЕ 6. Поиск по диапазону дат --------------------------------------
-- Сначала без индекса:
EXPLAIN ANALYZE SELECT * FROM events WHERE created_at >= NOW() - INTERVAL '1 day';

CREATE INDEX idx_events_created_at ON events(created_at);
ANALYZE events;

-- Затем те же и более широкие диапазоны — видно, где индекс перестаёт помогать:
EXPLAIN ANALYZE SELECT * FROM events WHERE created_at >= NOW() - INTERVAL '1 day';
EXPLAIN ANALYZE SELECT * FROM events WHERE created_at >= NOW() - INTERVAL '30 days';
EXPLAIN ANALYZE SELECT * FROM events WHERE created_at >= NOW() - INTERVAL '365 days';

-- Доля таблицы по каждому диапазону:
SELECT
    count(*) FILTER (WHERE created_at >= NOW() - INTERVAL '1 day')   AS d1,
    count(*) FILTER (WHERE created_at >= NOW() - INTERVAL '30 days') AS d30,
    count(*) FILTER (WHERE created_at >= NOW() - INTERVAL '365 days') AS d365,
    count(*)                                                          AS total
FROM events;


-- ЗАДАНИЕ 7. Фильтрация и сортировка -------------------------------------
-- Обратить внимание на узел Sort:
EXPLAIN ANALYZE
SELECT * FROM events WHERE user_id = 123 ORDER BY created_at DESC LIMIT 100;

CREATE INDEX idx_events_user_created ON events(user_id, created_at DESC);
ANALYZE events;

EXPLAIN ANALYZE
SELECT * FROM events WHERE user_id = 123 ORDER BY created_at DESC LIMIT 100;

-- Если Sort не исчез — вероятно, выбран Bitmap, который теряет порядок индекса.
-- Проверить можно так:
SET enable_bitmapscan = off;
EXPLAIN ANALYZE
SELECT * FROM events WHERE user_id = 123 ORDER BY created_at DESC LIMIT 100;
SET enable_bitmapscan = on;


-- ЗАДАНИЕ 8. Агрегация ---------------------------------------------------
EXPLAIN ANALYZE
SELECT event_type, COUNT(*)
FROM events
WHERE created_at >= NOW() - INTERVAL '30 days'
GROUP BY event_type;

-- Для сравнения — тот же запрос с запретом индексных путей:
SET enable_indexscan = off; SET enable_bitmapscan = off;
EXPLAIN ANALYZE
SELECT event_type, COUNT(*)
FROM events
WHERE created_at >= NOW() - INTERVAL '30 days'
GROUP BY event_type;
SET enable_indexscan = on;  SET enable_bitmapscan = on;


-- ЗАДАНИЕ 9. Стоимость индексов при вставке ------------------------------
-- Две одинаковые таблицы: одна без индексов, вторая с тремя.
DROP TABLE IF EXISTS events_plain, events_indexed;

CREATE TABLE events_plain   (LIKE events INCLUDING DEFAULTS INCLUDING IDENTITY);
CREATE TABLE events_indexed (LIKE events INCLUDING DEFAULTS INCLUDING IDENTITY);

CREATE INDEX ON events_indexed(user_id);
CREATE INDEX ON events_indexed(created_at);
CREATE INDEX ON events_indexed(user_id, created_at DESC);

-- В pgAdmin время выполнения показывается в правом нижнем углу.
INSERT INTO events_plain (user_id, event_type, payload, created_at)
SELECT (random()*100000)::bigint, 'MESSAGE', '{}'::jsonb,
       NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 200000);

INSERT INTO events_indexed (user_id, event_type, payload, created_at)
SELECT (random()*100000)::bigint, 'MESSAGE', '{}'::jsonb,
       NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 200000);


-- ЗАДАНИЕ 10. Размер индексов --------------------------------------------
SELECT indexrelname,
       pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
       idx_scan
FROM pg_stat_user_indexes
WHERE relname = 'events'
ORDER BY pg_relation_size(indexrelid) DESC;

SELECT pg_size_pretty(pg_relation_size('events')) AS data,
       pg_size_pretty(pg_indexes_size('events'))  AS indexes;


-- ЗАДАНИЕ 11. Когда индекс уже не спасает --------------------------------
-- Задание письменное, но запрос стоит выполнить: обратить внимание,
-- что DATE(created_at) — выражение, и обычный индекс по created_at
-- для группировки по нему не применяется.
EXPLAIN ANALYZE
SELECT DATE(created_at), COUNT(*)
FROM events
WHERE created_at >= NOW() - INTERVAL '365 days'
GROUP BY DATE(created_at);

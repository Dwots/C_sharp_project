-- ---------------------------------------------------------------------------
-- Лабораторная работа №2, задания 2-3: наращивание объёма и замер размера.
--
-- Таблица НЕ пересоздаётся — строки добавляются к уже существующим.
-- Контрольные точки проходятся последовательно, добавляя разницу:
--
--   -v add=10000     ->    10 000
--   -v add=90000     ->   100 000
--   -v add=900000    -> 1 000 000
--   -v add=4000000   -> 5 000 000
--   -v add=5000000   -> 10 000 000
--
-- Запуск:
--   docker exec -i gamelib_db psql -U gameuser -d gamelibdb -v add=90000 \
--       -f - < docs/lab-02/03_grow.sql
-- ---------------------------------------------------------------------------

\if :{?add}
\else
  \set add 10000
\endif

\timing on
\echo '>>> добавляется строк:' :add

INSERT INTO events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE
        WHEN random() < 0.4 THEN 'MESSAGE'
        WHEN random() < 0.7 THEN 'LOGIN'
        WHEN random() < 0.9 THEN 'PURCHASE'
        ELSE 'OTHER'
    END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, :add);

-- Без свежей статистики планировщик будет ошибаться в оценках.
ANALYZE events;

\timing off

-- Задание 3: размеры на текущей контрольной точке.
SELECT
    count(*)                                         AS rows,
    pg_size_pretty(pg_relation_size('events'))       AS table_size,
    pg_size_pretty(pg_total_relation_size('events')) AS total_size
FROM events;

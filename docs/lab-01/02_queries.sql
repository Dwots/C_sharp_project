-- ===========================================================================
-- Лабораторная работа №1 — запросы к заданиям 3-10.
--
-- Как пользоваться в pgAdmin:
--   выделить один запрос мышью и нажать F5. Без выделения F5 выполнит
--   весь файл, а в результатах останется только последний план.
-- ===========================================================================


-- ---------------------------------------------------------------------------
-- СБРОС. Выполнить, если хочется пройти лабу с чистого листа:
-- индексы из заданий 6, 7 и 9 уже созданы.
-- ---------------------------------------------------------------------------
-- DROP INDEX IF EXISTS idx_orders_user_id;
-- DROP INDEX IF EXISTS idx_orders_status;
-- DROP INDEX IF EXISTS idx_orders_created_at;
-- ANALYZE orders;

-- Проверить, какие индексы есть сейчас:
SELECT indexrelname,
       pg_size_pretty(pg_relation_size(indexrelid)) AS size,
       idx_scan
FROM pg_stat_user_indexes
WHERE relname = 'orders'
ORDER BY indexrelname;


-- ===========================================================================
-- ЗАДАНИЕ 3. EXPLAIN — только план, запрос не выполняется
-- ===========================================================================
EXPLAIN
SELECT * FROM orders WHERE user_id = 123;


-- ===========================================================================
-- ЗАДАНИЕ 4. EXPLAIN ANALYZE — план + фактические цифры
-- Смотреть: actual rows, Rows Removed by Filter, Execution Time
-- ===========================================================================
EXPLAIN ANALYZE
SELECT * FROM orders WHERE user_id = 123;


-- ===========================================================================
-- ЗАДАНИЕ 5. Sequential Scan
-- ===========================================================================
EXPLAIN ANALYZE
SELECT * FROM orders;

EXPLAIN ANALYZE
SELECT * FROM orders WHERE amount > 0;


-- ===========================================================================
-- ЗАДАНИЕ 6. Первый B-tree индекс
-- ===========================================================================
CREATE INDEX idx_orders_user_id ON orders(user_id);
ANALYZE orders;

EXPLAIN ANALYZE
SELECT * FROM orders WHERE user_id = 123;

-- Размер индекса — это его цена:
SELECT pg_size_pretty(pg_relation_size('idx_orders_user_id')) AS index_size;

-- Почему Bitmap, а не Index Scan? Сравнить, отключив битмап:
SET enable_bitmapscan = off;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123;
SET enable_bitmapscan = on;


-- ===========================================================================
-- ЗАДАНИЕ 7. Индекс используется не всегда
-- ===========================================================================
CREATE INDEX idx_orders_status ON orders(status);
ANALYZE orders;

EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'NEW';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'PAID';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'DELIVERED';
EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'CANCELLED';

-- Данные равномерные, поэтому все четыре дают ~25% и одинаковый план.
-- Чтобы увидеть отказ от индекса, нужно условие пошире:
EXPLAIN ANALYZE SELECT * FROM orders WHERE status IN ('NEW','PAID');  -- 50%
EXPLAIN ANALYZE SELECT * FROM orders WHERE status <> 'PAID';          -- 75% -> Seq Scan


-- ===========================================================================
-- ЗАДАНИЕ 8. Селективность
-- ===========================================================================
SELECT status,
       COUNT(*) AS rows,
       ROUND(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (), 2) AS pct
FROM orders
GROUP BY status
ORDER BY rows;


-- ===========================================================================
-- ЗАДАНИЕ 9. Range Query
-- Сначала выполнить ДО создания индекса, потом создать и повторить.
-- ===========================================================================
EXPLAIN ANALYZE
SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '7 days';

CREATE INDEX idx_orders_created_at ON orders(created_at);
ANALYZE orders;

EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '1 day';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '7 days';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '1 month';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '1 year';


-- ===========================================================================
-- ЗАДАНИЕ 10. Bitmap Scan
-- BUFFERS показывает, сколько страниц реально прочитано —
-- именно здесь видно, что при 25% битмап поднимает всю таблицу.
-- ===========================================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM orders WHERE status = 'NEW';

-- Всего страниц в таблице — сравнить с Heap Blocks из плана выше:
SELECT relpages AS table_blocks, reltuples::bigint AS rows
FROM pg_class WHERE relname = 'orders';

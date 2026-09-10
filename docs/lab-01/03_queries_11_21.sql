-- ===========================================================================
-- Лабораторная работа №1 — запросы к заданиям 11-21.
-- В pgAdmin: выделить один запрос и нажать F5.
-- ===========================================================================


-- ЗАДАНИЕ 11. Несколько индексов -------------------------------------------
-- Запрос из методички: BitmapAnd НЕ появится, user_id слишком селективен.
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 AND status = 'PAID';

-- А вот здесь появится: оба условия по отдельности дают ~20 тыс. строк,
-- а вместе — 408, и число страниц падает с ~9000 до 399.
CREATE INDEX IF NOT EXISTS idx_orders_amount ON orders(amount);
ANALYZE orders;

EXPLAIN ANALYZE
SELECT * FROM orders
WHERE created_at > NOW() - INTERVAL '15 days'
  AND amount BETWEEN 1000 AND 1200;


-- ЗАДАНИЕ 12. Составной индекс ---------------------------------------------
CREATE INDEX idx_orders_user_status ON orders(user_id, status);
ANALYZE orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 AND status = 'PAID';


-- ЗАДАНИЕ 13. Порядок колонок ----------------------------------------------
CREATE INDEX idx_orders_user_created_at ON orders(user_id, created_at);
ANALYZE orders;

EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 AND created_at > NOW() - INTERVAL '30 days';
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '30 days';

-- Ключевая проверка: убрать запасной индекс по created_at и повторить
-- третий запрос — составной (user_id, created_at) для него бесполезен.
DROP INDEX idx_orders_created_at;
ANALYZE orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '30 days';

CREATE INDEX idx_orders_created_at_user ON orders(created_at, user_id);
ANALYZE orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE created_at > NOW() - INTERVAL '30 days';


-- ЗАДАНИЕ 14. WHERE + ORDER BY ---------------------------------------------
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 ORDER BY created_at DESC;

CREATE INDEX idx_orders_user_created_at_desc ON orders(user_id, created_at DESC);
ANALYZE orders;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 ORDER BY created_at DESC;

-- Sort остаётся! Потому что выбран Bitmap, а он теряет порядок индекса.
-- Убрать Sort может только Index Scan:
SET enable_bitmapscan = off;
EXPLAIN ANALYZE SELECT * FROM orders WHERE user_id = 123 ORDER BY created_at DESC;
SET enable_bitmapscan = on;


-- ЗАДАНИЕ 15. Pagination ---------------------------------------------------
-- У user_id = 123 всего 9 заказов, LIMIT 20 ничего не ограничивает.
-- Показательный вариант — лента последних заказов по всей таблице:
EXPLAIN ANALYZE SELECT * FROM orders ORDER BY created_at DESC LIMIT 20;

-- То же самое без индексных путей — видно Sort по миллиону строк:
SET enable_indexscan = off; SET enable_bitmapscan = off;
EXPLAIN ANALYZE SELECT * FROM orders ORDER BY created_at DESC LIMIT 20;
SET enable_indexscan = on;  SET enable_bitmapscan = on;


-- ЗАДАНИЕ 16. Index Only Scan ----------------------------------------------
EXPLAIN ANALYZE SELECT id, user_id FROM orders WHERE user_id = 123;

CREATE INDEX idx_orders_user_id_include ON orders(user_id) INCLUDE (id, status, created_at);
-- VACUUM обязателен: без свежей карты видимости Heap Fetches будет > 0.
VACUUM ANALYZE orders;
EXPLAIN ANALYZE SELECT id, user_id FROM orders WHERE user_id = 123;


-- ЗАДАНИЕ 17. Partial Index ------------------------------------------------
EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'NEW' ORDER BY created_at;

CREATE INDEX idx_orders_new ON orders(created_at) WHERE status = 'NEW';
ANALYZE orders;
-- Не будет использован: NEW — это 25% таблицы, слишком много.
EXPLAIN ANALYZE SELECT * FROM orders WHERE status = 'NEW' ORDER BY created_at;

-- Проверка на данных, где предпосылка задания выполняется (редкий статус):
UPDATE orders SET status = 'REFUNDED' WHERE id % 1000 = 0;
CREATE INDEX idx_orders_refunded ON orders(created_at) WHERE status = 'REFUNDED';
VACUUM ANALYZE orders;

SELECT indexrelname, pg_size_pretty(pg_relation_size(indexrelid)) AS size
FROM pg_stat_user_indexes
WHERE indexrelname IN ('idx_orders_status','idx_orders_new','idx_orders_refunded');


-- ЗАДАНИЕ 18. Expression Index ---------------------------------------------
-- Таблица users в проекте занята, поэтому lab_users.
DROP TABLE IF EXISTS lab_users;
CREATE TABLE lab_users (id BIGSERIAL PRIMARY KEY, email VARCHAR(255) NOT NULL);
INSERT INTO lab_users (email) SELECT 'User' || s || '@Example.com' FROM generate_series(1, 200000) s;
INSERT INTO lab_users (email) VALUES ('Test@Example.com');
CREATE INDEX idx_lab_users_email ON lab_users(email);
ANALYZE lab_users;

-- Обычный индекс не подойдёт: в нём лежит email, а ищется LOWER(email).
EXPLAIN ANALYZE SELECT * FROM lab_users WHERE LOWER(email) = 'test@example.com';

CREATE INDEX idx_lab_users_lower_email ON lab_users(LOWER(email));
ANALYZE lab_users;
EXPLAIN ANALYZE SELECT * FROM lab_users WHERE LOWER(email) = 'test@example.com';


-- ЗАДАНИЕ 19. Цена индексов при вставке ------------------------------------
DROP TABLE IF EXISTS lab_ins_plain, lab_ins_indexed;

CREATE TABLE lab_ins_plain   (id BIGSERIAL PRIMARY KEY, user_id BIGINT, status VARCHAR(20),
                              amount NUMERIC(10,2), created_at TIMESTAMP);
CREATE TABLE lab_ins_indexed (id BIGSERIAL PRIMARY KEY, user_id BIGINT, status VARCHAR(20),
                              amount NUMERIC(10,2), created_at TIMESTAMP);

CREATE INDEX ON lab_ins_indexed(user_id);
CREATE INDEX ON lab_ins_indexed(status);
CREATE INDEX ON lab_ins_indexed(created_at);
CREATE INDEX ON lab_ins_indexed(amount);
CREATE INDEX ON lab_ins_indexed(user_id, status);

-- В pgAdmin время каждого запроса видно в правом нижнем углу.
INSERT INTO lab_ins_plain (user_id, status, amount, created_at)
SELECT (random()*100000)::bigint, (ARRAY['NEW','PAID','DELIVERED','CANCELLED'])[floor(random()*4+1)],
       random()*10000, NOW() - (random() * INTERVAL '2 years')
FROM generate_series(1, 200000);

INSERT INTO lab_ins_indexed (user_id, status, amount, created_at)
SELECT (random()*100000)::bigint, (ARRAY['NEW','PAID','DELIVERED','CANCELLED'])[floor(random()*4+1)],
       random()*10000, NOW() - (random() * INTERVAL '2 years')
FROM generate_series(1, 200000);


-- ЗАДАНИЕ 20. Неиспользуемые индексы ---------------------------------------
SELECT indexrelname, idx_scan, pg_size_pretty(pg_relation_size(indexrelid)) AS size
FROM pg_stat_user_indexes
WHERE relname = 'orders'
ORDER BY idx_scan, indexrelname;

-- Сколько всего весят индексы против самих данных:
SELECT pg_size_pretty(pg_relation_size('orders')) AS table_size,
       pg_size_pretty(pg_indexes_size('orders'))  AS indexes_size;


-- ЗАДАНИЕ 21. Финальная оптимизация ----------------------------------------
EXPLAIN ANALYZE
SELECT id, amount, status, created_at
FROM orders
WHERE user_id = 83888
  AND status = 'PAID'
  AND created_at >= NOW() - INTERVAL '30 days'
ORDER BY created_at DESC
LIMIT 50;

-- Порядок колонок: равенства вперёд, диапазон последним, DESC под ORDER BY.
CREATE INDEX idx_orders_user_status_created ON orders(user_id, status, created_at DESC);
ANALYZE orders;

EXPLAIN ANALYZE
SELECT id, amount, status, created_at
FROM orders
WHERE user_id = 83888
  AND status = 'PAID'
  AND created_at >= NOW() - INTERVAL '30 days'
ORDER BY created_at DESC
LIMIT 50;

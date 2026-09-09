-- Лабораторная работа №1, задания 1-2.
-- Тестовая песочница: отдельная от схемы проекта, к миграциям Liquibase
-- отношения не имеет. Запускать вручную, см. docs/lab-01-indexes.md.

DROP TABLE IF EXISTS orders;

CREATE TABLE orders (
    id         BIGSERIAL PRIMARY KEY,
    user_id    BIGINT        NOT NULL,
    product_id BIGINT        NOT NULL,
    status     VARCHAR(20)   NOT NULL,
    amount     NUMERIC(10,2) NOT NULL,
    created_at TIMESTAMP     NOT NULL,
    updated_at TIMESTAMP     NOT NULL
);

-- 1 000 000 строк. Занимает ~10-30 секунд.
INSERT INTO orders (user_id, product_id, status, amount, created_at, updated_at)
SELECT
    (random() * 100000)::BIGINT,
    (random() * 10000)::BIGINT,
    (ARRAY['NEW','PAID','DELIVERED','CANCELLED'])[floor(random() * 4 + 1)],
    random() * 10000,
    NOW() - (random() * INTERVAL '2 years'),
    NOW()
FROM generate_series(1, 1000000);

-- Обязательно: без свежей статистики планировщик будет ошибаться в оценках.
ANALYZE orders;

SELECT
    count(*)                                             AS rows,
    pg_size_pretty(pg_total_relation_size('orders'))     AS total_size
FROM orders;

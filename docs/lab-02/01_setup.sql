-- ---------------------------------------------------------------------------
-- Лабораторная работа №2, задание 1: создание таблицы events.
-- Данные НЕ генерирует — этим занимается 03_grow.sql.
-- ---------------------------------------------------------------------------

DROP TABLE IF EXISTS events;

CREATE TABLE events (
    id         BIGSERIAL PRIMARY KEY,
    user_id    BIGINT      NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload    JSONB,
    created_at TIMESTAMP   NOT NULL
);

SELECT 'events создана, строк: ' || count(*) FROM events;

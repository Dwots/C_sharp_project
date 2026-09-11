--liquibase formatted sql

--changeset kirill:8

-- Эндпоинт GET /api/users/{userId}/sessions.
-- Порядок колонок: равенство по user_id первым, время последним —
-- оно используется и как диапазон (?from/?to), и как ORDER BY.
-- DESC совпадает с порядком сортировки в запросе, что убирает узел Sort.
CREATE INDEX IF NOT EXISTS idx_play_sessions_user_created
    ON play_sessions (user_id, created_at DESC);

-- Эндпоинт GET /api/stats/top-games: агрегат по сессиям за период.
-- Отбор идёт только по времени, поэтому индекс одноколоночный.
CREATE INDEX IF NOT EXISTS idx_play_sessions_created_at
    ON play_sessions (created_at);

--rollback DROP INDEX idx_play_sessions_user_created;
--rollback DROP INDEX idx_play_sessions_created_at;

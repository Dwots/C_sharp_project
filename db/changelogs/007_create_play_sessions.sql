--liquibase formatted sql

--changeset kirill:7
-- Игровая сессия: один факт «пользователь играл в игру столько-то минут».
-- Это основная растущая сущность проекта (scaling entity): в отличие от
-- user_games, где на пару (user, game) приходится ровно одна строка,
-- сессий у той же пары может быть сколько угодно — они копятся каждый день.
--
-- Индексов здесь намеренно нет, кроме первичного ключа: их подбор и
-- обоснование — предмет лабораторной работы №1 (см. docs/lab-01-indexes.md).
CREATE TABLE IF NOT EXISTS play_sessions (
    id               BIGSERIAL PRIMARY KEY,
    user_id          INTEGER     NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id          INTEGER     NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    duration_minutes INTEGER     NOT NULL CHECK (duration_minutes > 0),
    -- Низкоселективное поле: значений мало, строк на каждое — много.
    platform         VARCHAR(20) NOT NULL,
    created_at       TIMESTAMP   NOT NULL DEFAULT NOW()
);

--rollback DROP TABLE play_sessions;

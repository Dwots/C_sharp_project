-- Схема одного шарда (лабораторная №5).
--
-- Шард хранит только распределяемую сущность — play_sessions. Справочники
-- users и games сюда не копируются: шардируется одна таблица, а не вся база.
-- По той же причине нет внешних ключей — целостность между шардами
-- PostgreSQL обеспечить не может, за ней следит приложение.
--
-- Скрипт выполняется автоматически при создании контейнера: образ postgres
-- запускает всё из /docker-entrypoint-initdb.d при инициализации пустого тома.

CREATE TABLE IF NOT EXISTS play_sessions (
    id               BIGINT      NOT NULL,
    user_id          INTEGER     NOT NULL,
    game_id          INTEGER     NOT NULL,
    duration_minutes INTEGER     NOT NULL,
    platform         VARCHAR(20) NOT NULL,
    created_at       TIMESTAMP   NOT NULL,
    PRIMARY KEY (id)
);

-- Основной запрос сервиса к шарду — история пользователя.
CREATE INDEX IF NOT EXISTS idx_shard_user_created
    ON play_sessions (user_id, created_at DESC);

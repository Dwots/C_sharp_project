--liquibase formatted sql

-- ===========================================================================
-- Перевод play_sessions на RANGE-партиционирование по created_at (лаб. №3).
--
-- Зачем перестраивать таблицу: превратить обычную таблицу в партиционированную
-- на месте PostgreSQL не умеет. Нужно создать новую партиционированную,
-- перелить данные и подменить.
--
-- Первичный ключ партиционированной таблицы обязан содержать ключ
-- партиционирования, поэтому PRIMARY KEY (id) становится (id, created_at).
--
-- Шаги разнесены по отдельным changeset'ам сознательно: внутри одного
-- PostgreSQL выполняет весь SQL одной пачкой, и построить индекс на партициях,
-- созданных в этой же пачке, нельзя.
--
-- На чистой базе таблица пуста, и вся миграция отрабатывает мгновенно.
-- ===========================================================================


--changeset kirill:9-1
-- Убираем старую таблицу с дороги вместе с последовательностью, освобождая имена.
ALTER TABLE play_sessions RENAME TO play_sessions_legacy;
ALTER SEQUENCE play_sessions_id_seq RENAME TO play_sessions_legacy_id_seq;

-- Переименование таблицы НЕ переименовывает её индексы: они сохраняют прежние
-- имена и не дадут создать одноимённые на новой таблице. Старая таблица всё
-- равно удаляется в конце, а для последовательного чтения при переливе индексы
-- не нужны, поэтому удаляем их сразу.
DROP INDEX IF EXISTS idx_play_sessions_user_created;
DROP INDEX IF EXISTS idx_play_sessions_created_at;
-- Покрывающий индекс из лабораторной №2, созданный вручную вне миграций.
DROP INDEX IF EXISTS idx_ps_created_cover;


--changeset kirill:9-2
-- Новая партиционированная таблица. Внешние ключи на партиционированной
-- таблице поддерживаются начиная с PostgreSQL 12.
CREATE TABLE play_sessions (
    id               BIGSERIAL   NOT NULL,
    user_id          INTEGER     NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id          INTEGER     NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    duration_minutes INTEGER     NOT NULL CHECK (duration_minutes > 0),
    platform         VARCHAR(20) NOT NULL,
    created_at       TIMESTAMP   NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);


--changeset kirill:9-3 splitStatements:false
-- Помесячные партиции: покрываем весь диапазон существующих данных и три
-- месяца вперёд от текущего.
--
-- DEFAULT-партиция намеренно НЕ создаётся. Она принимала бы строки за пределами
-- покрытого диапазона молча, и отсутствие будущих партиций осталось бы
-- незамеченным. Вместо этого наличие партиций контролирует PartitionHealthCheck:
-- лучше заранее получить алерт, чем копить данные в DEFAULT.
DO $$
DECLARE
    lo date;
    hi date;
    m  date;
BEGIN
    SELECT COALESCE(date_trunc('month', min(created_at))::date, date_trunc('month', now())::date),
           COALESCE(date_trunc('month', max(created_at))::date, date_trunc('month', now())::date)
      INTO lo, hi
      FROM play_sessions_legacy;

    hi := GREATEST(hi, (date_trunc('month', now()) + INTERVAL '3 months')::date);

    m := lo;
    WHILE m <= hi LOOP
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF play_sessions FOR VALUES FROM (%L) TO (%L)',
            'play_sessions_' || to_char(m, 'YYYY_MM'),
            m,
            (m + INTERVAL '1 month')::date);
        m := (m + INTERVAL '1 month')::date;
    END LOOP;
END $$;


--changeset kirill:9-4
-- Перелив данных. Индексы создаются после копирования — так быстрее.
INSERT INTO play_sessions (id, user_id, game_id, duration_minutes, platform, created_at)
SELECT id, user_id, game_id, duration_minutes, platform, created_at
FROM play_sessions_legacy;

-- Сдвигаем последовательность за максимальный существующий id.
SELECT setval('play_sessions_id_seq', GREATEST((SELECT COALESCE(max(id), 0) FROM play_sessions), 1));


--changeset kirill:9-5
-- Индексы из миграции 008. Созданные на родительской таблице, они
-- автоматически размножаются по всем партициям, включая будущие.
CREATE INDEX idx_play_sessions_user_created ON play_sessions (user_id, created_at DESC);
CREATE INDEX idx_play_sessions_created_at   ON play_sessions (created_at);


--changeset kirill:9-6
DROP TABLE play_sessions_legacy;
ANALYZE play_sessions;

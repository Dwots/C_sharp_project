-- ---------------------------------------------------------------------------
-- Массовая генерация тестовых данных (требование §15).
--
-- Объёмы задаются переменными psql, значения по умолчанию — ниже.
--   psql -v users=100000 -v games=5000 -v sessions=1000000 -f generate_data.sql
-- Либо через обёртку:  ./db/seed/generate.sh 100000 5000 1000000
--
-- Скрипт НЕ является миграцией Liquibase: он запускается вручную и только
-- на тестовом окружении, иначе миллион фейковых сессий уезжал бы в базу
-- при каждом старте проекта.
-- ---------------------------------------------------------------------------

\if :{?users}
\else
  \set users 10000
\endif
\if :{?games}
\else
  \set games 1000
\endif
\if :{?sessions}
\else
  \set sessions 1000000
\endif

\timing on

\echo '>>> users:' :users '| games:' :games '| sessions:' :sessions

-- --------------------------------------------------------------------------
-- Пользователи. Пароль у всех сгенерированных — "Admin123!" (тот же хэш,
-- что у админа), чтобы под любым из них можно было залогиниться и потыкать API.
-- --------------------------------------------------------------------------
INSERT INTO users (username, email, password_hash, role, created_at)
SELECT
    'user_' || s,
    'user_' || s || '@gamelib.local',
    '$2a$11$EWcKumG.yY3d.n8dIejCMeQdQZv1SPVUHIJC94WmTk3J2ed77IAl2',
    'User',
    NOW() - (random() * INTERVAL '2 years')
FROM generate_series(1, :users) s
ON CONFLICT (email) DO NOTHING;

-- --------------------------------------------------------------------------
-- Игры.
-- --------------------------------------------------------------------------
INSERT INTO games (title, description, price, developer, release_date, created_at)
SELECT
    'Game #' || s,
    'Автоматически сгенерированная игра для нагрузочных тестов',
    round((random() * 5999)::numeric, 2),
    (ARRAY['Valve','CD Projekt','FromSoftware','Rockstar','Ubisoft',
           'Bethesda','Capcom','Square Enix','Larian','Team Cherry'])[floor(random() * 10 + 1)],
    (NOW() - (random() * INTERVAL '15 years'))::date,
    NOW() - (random() * INTERVAL '2 years')
FROM generate_series(1, :games) s;

-- --------------------------------------------------------------------------
-- Связь игр с категориями (many-to-many): каждой игре 1-3 случайных жанра.
-- --------------------------------------------------------------------------
-- LIMIT зависит от g.id, а не от random(): иначе планировщик вычисляет
-- выражение один раз на весь запрос и каждой игре достаётся ровно один жанр.
INSERT INTO game_categories (game_id, category_id)
SELECT
    g.id,
    c.id
FROM games g
CROSS JOIN LATERAL (
    SELECT id FROM categories ORDER BY random() LIMIT (1 + g.id % 3)
) c
ON CONFLICT DO NOTHING;

-- --------------------------------------------------------------------------
-- Игровые сессии — основная растущая таблица.
-- Идентификаторы берём массивами: это устойчиво к «дыркам» в SERIAL после
-- удалений, в отличие от random() по диапазону min..max.
-- --------------------------------------------------------------------------
INSERT INTO play_sessions (user_id, game_id, duration_minutes, platform, created_at)
SELECT
    ids.u[1 + floor(random() * array_length(ids.u, 1))::int],
    ids.g[1 + floor(random() * array_length(ids.g, 1))::int],
    1 + floor(random() * 300)::int,
    (ARRAY['PC','PlayStation','Xbox','Switch'])[floor(random() * 4 + 1)],
    NOW() - (random() * INTERVAL '2 years')
FROM generate_series(1, :sessions) s
CROSS JOIN (
    SELECT
        (SELECT array_agg(id) FROM users) AS u,
        (SELECT array_agg(id) FROM games) AS g
) ids;

-- --------------------------------------------------------------------------
-- Библиотеки пользователей: каждому по ~12 случайных игр.
-- Генерируется независимо от сессий — иначе на 1 млн случайных пар
-- (user, game) почти не бывает совпадений и user_games раздувается
-- до размера самой play_sessions.
-- --------------------------------------------------------------------------
INSERT INTO user_games (user_id, game_id, rating, added_at)
SELECT
    user_id,
    game_id,
    1 + floor(random() * 10)::int,
    NOW() - (random() * INTERVAL '2 years')
FROM (
    SELECT DISTINCT
        u.id AS user_id,
        ids.g[1 + floor(random() * array_length(ids.g, 1))::int] AS game_id
    FROM users u
    CROSS JOIN generate_series(1, 12) k
    CROSS JOIN (SELECT array_agg(id) AS g FROM games) ids
) pairs
ON CONFLICT (user_id, game_id) DO NOTHING;

-- --------------------------------------------------------------------------
-- Без свежей статистики планировщик будет ошибаться в оценках,
-- и все замеры EXPLAIN ANALYZE окажутся недостоверными.
-- --------------------------------------------------------------------------
ANALYZE users;
ANALYZE games;
ANALYZE categories;
ANALYZE game_categories;
ANALYZE user_games;
ANALYZE play_sessions;

\timing off

SELECT
    (SELECT count(*) FROM users)           AS users,
    (SELECT count(*) FROM games)           AS games,
    (SELECT count(*) FROM game_categories) AS game_categories,
    (SELECT count(*) FROM user_games)      AS user_games,
    (SELECT count(*) FROM play_sessions)   AS play_sessions,
    pg_size_pretty(pg_total_relation_size('play_sessions')) AS play_sessions_size;

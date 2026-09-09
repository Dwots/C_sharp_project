--liquibase formatted sql

--changeset kirill:2
CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT
);

-- Базовый набор жанров: нужен, чтобы поле categoryIds
-- при создании игры было к чему привязать.
INSERT INTO categories (name, description)
VALUES
    ('Action',     'Динамичные игры с упором на реакцию и бой'),
    ('Adventure',  'Приключения с исследованием мира и сюжетом'),
    ('RPG',        'Ролевые игры с прокачкой персонажа'),
    ('Strategy',   'Стратегии: управление ресурсами и планирование'),
    ('Simulation', 'Симуляторы реальных процессов и систем'),
    ('Sports',     'Спортивные игры и гонки'),
    ('Puzzle',     'Головоломки и логические игры'),
    ('Horror',     'Хорроры и survival horror'),
    ('Indie',      'Инди-игры от небольших студий'),
    ('Shooter',    'Шутеры от первого и третьего лица');

--rollback DROP TABLE categories;

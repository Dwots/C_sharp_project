--liquibase formatted sql

--changeset kirill:3
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'User',
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

--rollback DROP TABLE users;

--Пароль: "Admin123!"
INSERT INTO users (username, email, password_hash, role, created_at)
VALUES (
       'admin',
       'admin@gamelib.com',
       '$2a$11$EWcKumG.yY3d.n8dIejCMeQdQZv1SPVUHIJC94WmTk3J2ed77IAl2',
       'Admin',
       NOW()
       )
ON CONFLICT (email) DO NOTHING;
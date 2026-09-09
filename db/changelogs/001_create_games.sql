--liquibase formatted sql

--changeset kirill:1
CREATE TABLE IF NOT EXISTS games (
    id SERIAL PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    price DECIMAL(10,2) NOT NULL DEFAULT 0,
    developer VARCHAR(200),
    release_date DATE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

--rollback DROP TABLE games;
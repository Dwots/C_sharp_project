--liquibase formatted sql

--changeset kirill:2
CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT
);

--rollback DROP TABLE categories;
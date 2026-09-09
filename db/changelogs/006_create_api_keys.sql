--liquibase formatted sql

--changeset kirill:6
CREATE TABLE IF NOT EXISTS api_keys (
                                        id SERIAL PRIMARY KEY,
                                        key_hash VARCHAR(255) NOT NULL UNIQUE, -- Оставляем key_hash
    name VARCHAR(100) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'User',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    expires_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
    );

--rollback DROP TABLE api_keys;

-- Исправляем INSERT (используем key_hash вместо key)
INSERT INTO api_keys (key_hash, name, is_active, created_at)
VALUES ('dev-api-key-12345', 'Development Key', true, NOW());
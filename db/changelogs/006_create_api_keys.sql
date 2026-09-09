--liquibase formatted sql

--changeset kirill:6
CREATE TABLE IF NOT EXISTS api_keys (
    id SERIAL PRIMARY KEY,
    -- Учебный проект: ключ хранится как есть, поиск идёт по точному совпадению.
    key_hash VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'User',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    expires_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Ключ для разработки. Роль Admin указана явно: без неё сработал бы
-- DEFAULT 'User', и ключ мог бы только читать (POST/PUT/DELETE -> 403).
INSERT INTO api_keys (key_hash, name, role, is_active, created_at)
VALUES ('dev-api-key-12345', 'Development Key', 'Admin', true, NOW())
ON CONFLICT (key_hash) DO NOTHING;

--rollback DROP TABLE api_keys;

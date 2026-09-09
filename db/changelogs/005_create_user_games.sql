--liquibase formatted sql

--changeset kirill:5
CREATE TABLE IF NOT EXISTS user_games (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    rating INTEGER CHECK (rating IS NULL OR (rating >= 1 AND rating <= 10)),
    added_at TIMESTAMP NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, game_id)
    );

--rollback DROP TABLE user_games;
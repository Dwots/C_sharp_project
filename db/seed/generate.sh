#!/usr/bin/env bash
# Обёртка над generate_data.sql: заливает тестовые данные в БД проекта.
#
#   ./db/seed/generate.sh                        # 10 000 / 1 000 / 1 000 000
#   ./db/seed/generate.sh 100000 5000 5000000    # свои объёмы
#
# Требует запущенного контейнера gamelib_db (docker compose up -d postgres).
set -euo pipefail

USERS=${1:-10000}
GAMES=${2:-1000}
SESSIONS=${3:-1000000}

CONTAINER=${DB_CONTAINER:-gamelib_db}
DB_USER=${DB_USER:-gameuser}
DB_NAME=${DB_NAME:-gamelibdb}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! docker ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
    echo "Контейнер $CONTAINER не запущен. Сначала: docker compose up -d postgres" >&2
    exit 1
fi

echo "Генерация: users=$USERS games=$GAMES sessions=$SESSIONS"

docker exec -i "$CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" \
    -v users="$USERS" -v games="$GAMES" -v sessions="$SESSIONS" \
    -f - < "$SCRIPT_DIR/generate_data.sql"

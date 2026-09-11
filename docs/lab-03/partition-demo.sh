#!/usr/bin/env bash
# ===========================================================================
# Демонстрация сценария из лабораторной №3, части 10-11:
#   партиции -> сбой -> ALERT -> восстановление -> OK
#
# Каждый шаг запускается отдельной командой, чтобы на защите можно было
# комментировать происходящее и показывать Telegram между шагами.
#
#   ./docs/lab-03/partition-demo.sh status   — что есть сейчас
#   ./docs/lab-03/partition-demo.sh break    — уронить партицию (имитация сбоя)
#   ./docs/lab-03/partition-demo.sh check    — проверка + алерт при смене состояния
#   ./docs/lab-03/partition-demo.sh fix      — создать недостающие
#   ./docs/lab-03/partition-demo.sh reset    — сбросить память алертов
#   ./docs/lab-03/partition-demo.sh full     — весь сценарий с паузами
# ===========================================================================
set -uo pipefail

API=${API:-http://localhost:5000}
KEY=${API_KEY:-dev-api-key-12345}
SCHEMA=${SCHEMA:-lab03}
TABLE=${TABLE:-events}

B=$'\e[1m'; R=$'\e[31m'; G=$'\e[32m'; Y=$'\e[33m'; C=$'\e[36m'; N=$'\e[0m'

hdr() { printf '\n%s%s%s\n' "$B$C" "$1" "$N"; }
die() { printf '%s%s%s\n' "$R" "$1" "$N" >&2; exit 1; }

check_api() {
    curl -sf -o /dev/null "$API/health" 2>/dev/null \
        || die "API не отвечает на $API. Запустите: docker compose up -d"
}

# Показывает статус всех настроенных таблиц одной таблицей.
show_status() {
    curl -s -X POST "$API/api/partitions/check" -H "X-API-KEY: $KEY" \
    | python3 -c '
import sys, json
G="\033[32m"; R="\033[31m"; Y="\033[33m"; N="\033[0m"
try:
    rows = json.load(sys.stdin)
except Exception:
    print("не удалось разобрать ответ API"); sys.exit(1)
for r in rows:
    color = G if r["status"] == "OK" else R
    print("  %-24s %s%-9s%s  alertSent=%s" % (r["table"], color, r["status"], N, r["alertSent"]))
    for m in r["missing"]:
        print("      %sотсутствует: %s%s" % (Y, m, N))
    if not r["alertChannelConfigured"]:
        print("      %sканал алертов не настроен — сообщения только в лог%s" % (Y, N))
'
}

# Имя партиции на последний день горизонта: её и роняем.
horizon_partition() {
    python3 -c "
import datetime
d = datetime.date.today() + datetime.timedelta(days=3)
print('${TABLE}_%s' % d.strftime('%Y_%m_%d'))
"
}

cmd_status() {
    hdr "Текущее состояние партиций"
    show_status
}

cmd_break() {
    local p; p=$(horizon_partition)
    hdr "Имитация сбоя: удаляю партицию $p"
    local code
    code=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE \
        "$API/api/partitions/$SCHEMA/$p" -H "X-API-KEY: $KEY")
    if [ "$code" = "204" ]; then
        printf '  %sпартиция %s удалена%s\n' "$Y" "$p" "$N"
        printf '  ночная job как будто не отработала\n'
    else
        die "  не удалось удалить партицию, HTTP $code"
    fi
}

cmd_check() {
    hdr "PartitionHealthCheck"
    show_status
    printf '\n  %sуведомление уходит только при СМЕНЕ состояния%s\n' "$Y" "$N"
    printf '  %sповторный CRITICAL молчит, переход в OK шлёт recovery%s\n' "$Y" "$N"
}

cmd_fix() {
    hdr "Восстановление: создаю недостающие партиции"
    curl -s -X POST "$API/api/partitions/create-missing" -H "X-API-KEY: $KEY" \
    | python3 -c '
import sys, json
G="\033[32m"; N="\033[0m"
for r in json.load(sys.stdin):
    created = r["created"]
    print("  %-24s существует=%-3s нужно=%-3s создано=%s%s%s"
          % (r["table"], r["existingCount"], r["requiredCount"],
             G if created else "", created if created else "нечего", N))
    if r.get("error"):
        print("      ошибка: %s" % r["error"])
'
}

cmd_reset() {
    hdr "Сброс запомненного состояния алертов"
    curl -s -o /dev/null -X POST "$API/api/partitions/reset-alert-state" -H "X-API-KEY: $KEY"
    printf '  следующая проверка отправит уведомление заново\n'
}

pause() {
    printf '\n%s— %s. Enter, чтобы продолжить —%s' "$B" "$1" "$N"
    read -r _
}

cmd_full() {
    cmd_reset
    cmd_status
    pause "смотрим: всё на месте, состояние OK"

    cmd_break
    pause "партиция удалена, проверка ещё не запускалась"

    cmd_check
    pause "CRITICAL, alertSent=True — проверьте Telegram: должно прийти 🚨"

    hdr "Повторная проверка (дважды)"
    show_status
    show_status
    pause "alertSent=False — повторные уведомления подавлены, в Telegram тихо"

    cmd_fix
    cmd_check
    printf '\n  %sOK, alertSent=True — в Telegram пришло 🟢 recovery%s\n' "$G" "$N"

    hdr "Контрольная проверка"
    show_status
    printf '\n  %salertSent=False — восстановление тоже не дублируется%s\n\n' "$G" "$N"
}

check_api
case "${1:-full}" in
    status) cmd_status ;;
    break)  cmd_break  ;;
    check)  cmd_check  ;;
    fix)    cmd_fix    ;;
    reset)  cmd_reset  ;;
    full)   cmd_full   ;;
    *) die "Неизвестная команда: $1. Доступны: status, break, check, fix, reset, full" ;;
esac

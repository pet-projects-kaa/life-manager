#!/bin/bash
set -euo pipefail
BASE_URL="${1:-http://127.0.0.1:5099}"
COOKIE_JAR="${TMPDIR:-/tmp}/life-manager-smoke-cookies.txt"
rm -f "$COOKIE_JAR"

curl -fsS "$BASE_URL/health" | grep -q '"status":"ok"'
curl -fsS "$BASE_URL/" | grep -q 'Life Manager'
curl -fsS "$BASE_URL/app/" | grep -q 'Life Manager'

EMAIL="smoke-$(date +%s)-$RANDOM@example.test"
curl -fsS -c "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"SmokePass123!\",\"displayName\":\"Smoke\"}" \
  "$BASE_URL/api/auth/register" | grep -q '"displayName":"Smoke"'

curl -fsS -b "$COOKIE_JAR" -X POST "$BASE_URL/api/demo/seed" >/dev/null
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/dashboard" | grep -q '"profile"'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/tasks" | grep -q 'Отправить документы'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/habits" | grep -q 'Вода'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/shopping" | grep -q 'Молоко'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/home" | grep -q 'Сменить полотенца'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/benefits" | grep -q 'налоговый вычет'
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/advice" | grep -q '"title"'
curl -fsS -b "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d '{"category":"delivery","text":"Заказ должен был приехать неделю назад"}' \
  "$BASE_URL/api/legal/advice" | grep -q 'Проблема с доставкой'

echo "Smoke test passed"

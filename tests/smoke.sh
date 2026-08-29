#!/bin/bash
set -euo pipefail
BASE_URL="${1:-http://127.0.0.1:5099}"
COOKIE_JAR="${TMPDIR:-/tmp}/life-manager-smoke-cookies.txt"
rm -f "$COOKIE_JAR"

check_get() {
  local name="$1" url="$2" pattern="$3"
  echo "[smoke] $name: GET $url"
  curl -fsS "$url" | grep -q "$pattern"
}

check_get "health" "$BASE_URL/health" '"status":"ok"'
check_get "landing" "$BASE_URL/" 'Life Manager'
check_get "app shell" "$BASE_URL/app/" 'Life Manager'

EMAIL="smoke-$(date +%s)-$RANDOM@example.test"
echo "[smoke] register"
curl -fsS -c "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"SmokePass123!\",\"displayName\":\"Smoke\"}" \
  "$BASE_URL/api/auth/register" | grep -q '"displayName":"Smoke"'

echo "[smoke] seed demo"
curl -fsS -b "$COOKIE_JAR" -X POST "$BASE_URL/api/demo/seed" >/dev/null
check_get_auth() {
  local name="$1" url="$2" pattern="$3"
  echo "[smoke] $name: GET $url"
  curl -fsS -b "$COOKIE_JAR" "$url" | grep -q "$pattern"
}
check_get_auth "dashboard" "$BASE_URL/api/dashboard" '"profile"'
check_get_auth "tasks" "$BASE_URL/api/tasks" 'Отправить документы'
check_get_auth "habits" "$BASE_URL/api/habits" 'Вода'
check_get_auth "shopping" "$BASE_URL/api/shopping" 'Молоко'
check_get_auth "home" "$BASE_URL/api/home" 'Сменить полотенца'
check_get_auth "benefits" "$BASE_URL/api/benefits" 'налоговый вычет'
check_get_auth "advice" "$BASE_URL/api/advice" '"title"'

echo "[smoke] legal advice"
curl -fsS -b "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d '{"category":"delivery","text":"Заказ должен был приехать неделю назад"}' \
  "$BASE_URL/api/legal/advice" | grep -q 'Проблема с доставкой'

echo "Smoke test passed"

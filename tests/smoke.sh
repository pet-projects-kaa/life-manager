#!/bin/bash
set -euo pipefail

BASE_URL="${1:-http://127.0.0.1:5099}"
COOKIE_JAR="${TMPDIR:-/tmp}/life-manager-smoke-cookies.txt"
rm -f "$COOKIE_JAR"

assert_contains() {
  local name="$1" body="$2" pattern="$3"
  if ! grep -Fq -- "$pattern" <<<"$body"; then
    echo "[smoke] FAIL: $name did not contain expected text: $pattern" >&2
    echo "[smoke] response (first 2000 chars):" >&2
    printf '%s\n' "${body:0:2000}" >&2
    exit 1
  fi
}

check_get() {
  local name="$1" url="$2" pattern="$3" body
  echo "[smoke] $name: GET $url"
  body="$(curl -fsS "$url")"
  assert_contains "$name" "$body" "$pattern"
}

check_get_auth() {
  local name="$1" url="$2" pattern="$3" body
  echo "[smoke] $name: GET $url"
  body="$(curl -fsS -b "$COOKIE_JAR" "$url")"
  assert_contains "$name" "$body" "$pattern"
}

check_get "health" "$BASE_URL/health" '"status":"ok"'
check_get "landing" "$BASE_URL/" 'Life Manager'
check_get "app shell" "$BASE_URL/app/" 'Life Manager'

EMAIL="smoke-$(date +%s)-$RANDOM@example.test"
echo "[smoke] register"
REGISTER_BODY="$(curl -fsS -c "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"SmokePass123!\",\"displayName\":\"Smoke\"}" \
  "$BASE_URL/api/auth/register")"
assert_contains "register" "$REGISTER_BODY" '"displayName":"Smoke"'

# Create real data instead of a demo/test seed endpoint.
echo "[smoke] create task"
curl -fsS -b "$COOKIE_JAR" -H 'Content-Type: application/json' \
  -d '{"title":"Smoke task","notes":"ci","dueAt":null,"priority":"normal","repeatEveryDays":null}' \
  "$BASE_URL/api/tasks" >/dev/null

echo "[smoke] create habit"
curl -fsS -b "$COOKIE_JAR" -H 'Content-Type: application/json' \
  -d '{"title":"Smoke habit","icon":"✓","target":2,"unit":"раз"}' \
  "$BASE_URL/api/habits" >/dev/null

echo "[smoke] create shopping item"
curl -fsS -b "$COOKIE_JAR" -H 'Content-Type: application/json' \
  -d '{"title":"Smoke milk","category":"food","estimatedPrice":100}' \
  "$BASE_URL/api/shopping" >/dev/null

check_get_auth "dashboard" "$BASE_URL/api/dashboard" '"profile"'
check_get_auth "tasks" "$BASE_URL/api/tasks" 'Smoke task'
check_get_auth "habits" "$BASE_URL/api/habits" 'Smoke habit'
check_get_auth "shopping" "$BASE_URL/api/shopping" 'Smoke milk'
check_get_auth "advice" "$BASE_URL/api/advice" '"title"'

echo "[smoke] legal advice"
LEGAL_BODY="$(curl -fsS -b "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d '{"category":"auto","text":"Интернет-магазин задержал доставку заказа на неделю, хочу вернуть деньги"}' \
  "$BASE_URL/api/legal/advice")"
assert_contains "legal advice title" "$LEGAL_BODY" 'Задержка или проблема с доставкой'
assert_contains "legal advice source" "$LEGAL_BODY" 'consultant.ru'

echo "Smoke test passed"

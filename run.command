#!/bin/bash
set -e
cd "$(dirname "$0")"
if ! command -v docker >/dev/null 2>&1; then
  echo "Docker не найден. Установи Docker Desktop и запусти файл снова."
  read -r -p "Нажми Enter, чтобы закрыть..."
  exit 1
fi
echo "Life Manager: http://localhost:5086"
docker compose -f compose.local.yml up --build

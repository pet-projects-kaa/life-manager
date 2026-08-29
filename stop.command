#!/bin/bash
set -e
cd "$(dirname "$0")"
docker compose -f compose.local.yml down

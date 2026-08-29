#!/bin/bash
set -euo pipefail
node --check src/LifeManager.Api/wwwroot/app/app.js
node --check src/LifeManager.Api/wwwroot/app/sw.js
if command -v dotnet >/dev/null 2>&1; then
  dotnet restore LifeManager.sln --nologo
  dotnet build LifeManager.sln -c Release --no-restore --nologo
else
  echo "dotnet SDK not found: skipped .NET compile check"
fi
echo "Validation completed"

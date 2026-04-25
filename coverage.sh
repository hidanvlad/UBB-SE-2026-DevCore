#!/bin/bash
set -e

REPORT_DIR="./coverage/report"

echo "Running tests with coverage..."
dotnet test DevCoreHospital/DevCoreHospital.Tests \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

echo "Installing reportgenerator (skipped if already installed)..."
dotnet tool install -g dotnet-reportgenerator-globaltool 2>/dev/null || true

echo "Generating HTML report..."
"$USERPROFILE/.dotnet/tools/reportgenerator" \
  -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"$REPORT_DIR" \
  -reporttypes:Html

echo "Opening report..."
start "$REPORT_DIR/index.html"

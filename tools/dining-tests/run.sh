#!/bin/bash
# Compile the pure Dining classes (DiningModels / DiningPricing / DiningTokens / DiningNotifyText) + tests with Roslyn (C# 7.3)
# and run on mono. No DB / System.Web dependency. Usage: tools/dining-tests/run.sh   (from repo root or anywhere)
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SRC="$ROOT/Take Time BangPhra/Class/Dining"
API=/usr/lib/mono/4.7.2-api
CSC=/usr/lib/dotnet/sdk/8.0.131/Roslyn/bincore/csc.dll
OUT="${TMPDIR:-/tmp}/dining_tests_$$.exe"

if [ -f "$CSC" ] && command -v dotnet >/dev/null 2>&1; then
  dotnet "$CSC" -nologo -noconfig -nostdlib+ -langversion:7.3 -out:"$OUT" \
    -r:"$API/mscorlib.dll" -r:"$API/System.dll" -r:"$API/System.Core.dll" -r:"$API/Facades/System.Runtime.dll" \
    "$SRC/DiningModels.cs" "$SRC/DiningPricing.cs" "$SRC/DiningTokens.cs" "$SRC/DiningNotifyText.cs" "$HERE/DiningTests.cs"
else
  mcs -nologo -langversion:7.2 -out:"$OUT" -r:System.dll -r:System.Core.dll \
    "$SRC/DiningModels.cs" "$SRC/DiningPricing.cs" "$SRC/DiningTokens.cs" "$SRC/DiningNotifyText.cs" "$HERE/DiningTests.cs"
fi
mono "$OUT"
RC=$?
rm -f "$OUT"
exit $RC

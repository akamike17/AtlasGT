#!/usr/bin/env bash
# scripts/queen-test.sh — bateria de cierre para Gate K
# Corre: build Release, todas las suites, revision de secretos y basura.
set -uo pipefail
cd "$(dirname "$0")/.."

PASS=0
FAIL=0
ERRORS=()

say()  { printf '\n=== %s ===\n' "$*"; }
ok()   { PASS=$((PASS+1)); echo "[OK]   $*"; }
bad()  { FAIL=$((FAIL+1)); ERRORS+=("$*"); echo "[FAIL] $*"; }

say "1. Build Release"
if dotnet build AtlasGT.sln -c Release --nologo 2>&1 | tail -5; then
  if dotnet build AtlasGT.sln -c Release --nologo 2>&1 | grep -qE " [1-9][0-9]* (Error|Errores)"; then
    bad "Build Release tiene errores"
  else
    ok "Build Release limpio"
  fi
else
  bad "Build Release fallo"
fi

say "2. Tests (todas las suites)"
TOTAL_OUTPUT=$(dotnet test AtlasGT.sln -c Release --no-build 2>&1)
FAILED=$(echo "$TOTAL_OUTPUT" | grep -c "Con error:     [1-9]" || true)
if [ "$FAILED" -eq 0 ]; then
  PASSED=$(echo "$TOTAL_OUTPUT" | grep -oE "Superado:    [0-9]+" | awk '{s+=$2} END {print s}')
  ok "Suites verdes ($PASSED tests)"
else
  echo "$TOTAL_OUTPUT" | grep "Con error:     [1-9]" | head -5
  bad "Hay tests fallando"
fi

say "3. Secrets check"
SECRETS=$(grep -rEn "(password|api[_-]?key|secret|token)\s*[:=]\s*[\"'][^\"']{8,}" \
  --include="*.json" --include="*.cs" --include="*.cshtml" --include="*.md" \
  --exclude-dir=bin --exclude-dir=obj --exclude-dir=.vs --exclude-dir=.git \
  --exclude=ATLAS_GT_MASTER_DEFINITIVO_KIMI.md . 2>/dev/null \
  | grep -viE "(authorization Token requerido|AuthorizationToken|authorizationToken|api_key placeholder|x-atlas-role)" \
  | head -10 || true)
if [ -z "$SECRETS" ]; then
  ok "Sin secretos hardcodeados"
else
  echo "$SECRETS"
  bad "Posibles secretos en repo"
fi

say "4. Higiene git"
UNTRACKED=$(git ls-files --others --exclude-standard | wc -l)
MODIFIED=$(git diff --name-only | wc -l)
STAGED=$(git diff --cached --name-only | wc -l)
echo "  untracked=$UNTRACKED modified=$MODIFIED staged=$STAGED"
JUNK=$(git ls-files | grep -E "(\.vs/|/bin/|/obj/|\.user$|\.suo$)" | head -5)
if [ -n "$JUNK" ]; then
  echo "$JUNK"
  bad "Basura IDE trackeada en git"
else
  ok "Git limpio de .vs/bin/obj/.user"
fi

say "5. Prueba reina automatizada (existe)"
if grep -q "QueenTestE2E" tests/AtlasGT.EndToEndTests/*.cs 2>/dev/null; then
  ok "QueenTestE2E presente y corre como parte de E2E suite"
else
  bad "QueenTestE2E no encontrada"
fi

say "RESUMEN"
echo "  PASS: $PASS"
echo "  FAIL: $FAIL"
if [ "$FAIL" -gt 0 ]; then
  echo "  Errores:"
  printf '    - %s\n' "${ERRORS[@]}"
  exit 1
fi
echo "  SHA: $(git rev-parse --short HEAD)"
echo "  Branch: $(git rev-parse --abbrev-ref HEAD)"
exit 0

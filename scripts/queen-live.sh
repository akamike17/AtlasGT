#!/usr/bin/env bash
# scripts/queen-live.sh — prueba reina como PROCESO real:
# levanta AtlasGT.Api + AtlasGT.Web como procesos OS, hace flujo HTTP,
# verifica observaciones/auditoria/backup, tumba procesos al final.
#
# Esto NO reemplaza al QueenTestE2E in-process (dll); lo complementa.
# Salida final: exit 0 si todo ok; 1 si algo falla.
set -uo pipefail
cd "$(dirname "$0")/.."

API_PORT="${API_PORT:-5101}"
WEB_PORT="${WEB_PORT:-5102}"
DATA_DIR="$(mktemp -d -t atlasgt-live-XXXXXX)"
API_URL="http://127.0.0.1:$API_PORT"
WEB_URL="http://127.0.0.1:$WEB_PORT"
H_ADMIN=( -H "X-Atlas-Role: admin" -H "Content-Type: application/json" )
H_LAB=( -H "X-Atlas-Role: lab" -H "Content-Type: application/json" )
H_OP=( -H "X-Atlas-Role: operator" -H "Content-Type: application/json" )

API_PID=""
WEB_PID=""
cleanup() {
  [ -n "$API_PID" ] && kill "$API_PID" 2>/dev/null || true
  [ -n "$WEB_PID" ] && kill "$WEB_PID" 2>/dev/null || true
  rm -rf "$DATA_DIR" 2>/dev/null || true
}
trap cleanup EXIT

PASS=0; FAIL=0
ok()   { PASS=$((PASS+1)); echo "[OK]   $*"; }
bad()  { FAIL=$((FAIL+1)); echo "[FAIL] $*"; }

say "Preparando API y Web en el mismo dataRoot: $DATA_DIR" 2>/dev/null || true
REPO_NATIVE="C:/Users/Admin/source/repos/AtlasGT"

echo "== Build Release (por si acaso) =="
dotnet build AtlasGT.sln -c Release --nologo -v q 2>&1 | tail -3 || { echo "build fail"; exit 1; }

echo "== Levantar AtlasGT.Api en $API_URL =="
AtlasGT__DataRoot="$DATA_DIR" \
ASPNETCORE_URLS="http://127.0.0.1:$API_PORT" \
dotnet "$REPO_NATIVE/src/AtlasGT.Api/bin/Release/net8.0/AtlasGT.Api.dll" >"$DATA_DIR/api.log" 2>&1 &
API_PID=$!

echo "== Levantar AtlasGT.Web en $WEB_URL =="
AtlasGt__ApiBaseUrl="$API_URL" \
ASPNETCORE_URLS="http://127.0.0.1:$WEB_PORT" \
dotnet "$REPO_NATIVE/src/AtlasGT.Web/bin/Release/net8.0/AtlasGT.Web.dll" >"$DATA_DIR/web.log" 2>&1 &
WEB_PID=$!

# Esperar readiness
for i in {1..30}; do
  curl -sf "$API_URL/health" >/dev/null 2>&1 && break
  sleep 1
done
curl -sf "$API_URL/health" >/dev/null && ok "API /health" || { bad "API /health no responde"; echo "--- api.log ---"; tail -30 "$DATA_DIR/api.log"; exit 1; }

for i in {1..30}; do
  curl -sf "$WEB_URL/health" >/dev/null 2>&1 && break
  sleep 1
done
curl -sf "$WEB_URL/health" >/dev/null && ok "Web /health" || { bad "Web /health no responde"; echo "--- web.log ---"; tail -30 "$DATA_DIR/web.log"; exit 1; }

echo
echo "== 1) Crear Asset via API (con rol admin) =="
ASSET_JSON=$(curl -sf -X POST "$API_URL/api/assets" "${H_ADMIN[@]}" \
  -d '{"name":"PRENSA-04 (queen)","tag":"PR-QUEEN-01"}')
[ -n "$ASSET_JSON" ] && ok "POST /api/assets" || bad "POST /api/assets"
ASSET_ID=$(echo "$ASSET_JSON" | python -c 'import json,sys; print(json.load(sys.stdin)["id"])')
echo "  asset id: $ASSET_ID"

echo "== 2) Crear Endpoint asociado =="
EP_JSON=$(curl -sf -X POST "$API_URL/api/endpoints" "${H_ADMIN[@]}" \
  -d '{"name":"sandbox-tcp","address":"tcp://127.0.0.1:9999"}')
[ -n "$EP_JSON" ] && ok "POST /api/endpoints" || bad "POST /api/endpoints"
EP_ID=$(echo "$EP_JSON" | python -c 'import json,sys; print(json.load(sys.stdin)["id"])')

echo "== 3) Crear regla de alarma (warning > 50) =="
curl -sf -X POST "$API_URL/api/alarms/rules" "${H_ADMIN[@]}" \
  -d '{"name":"Temp alta","signalKey":"temp","threshold":50,"comparison":"GreaterThan","severity":"Warning","debounce":1}' >/dev/null \
  && ok "POST /api/alarms/rules" || bad "POST /api/alarms/rules"

echo "== 4) Publicar observaciones que excedan el umbral (sandbox, rol lab) =="
for i in 1 2 3; do
  RESP=$(curl -s -w "\nHTTP:%{http_code}" -X POST "$API_URL/api/sandbox/publish" "${H_LAB[@]}" \
    -d "{\"signalKey\":\"temp\",\"value\":$((55+i)),\"unit\":\"degC\"}")
  CODE=$(echo "$RESP" | grep "^HTTP:" | cut -d: -f2)
  if [ "$CODE" = "202" ]; then
    echo "  obs $i publicada"
  else
    echo "  obs $i FAIL ($CODE): $(echo "$RESP" | head -3)"
  fi
done
sleep 1

echo "== 5) Verificar alarma esta activa =="
ALARMS=$(curl -sf "$API_URL/api/alarms/active" -H "X-Atlas-Role: viewer")
CNT=$(echo "$ALARMS" | python -c 'import json,sys; print(len(json.load(sys.stdin)))' 2>/dev/null || echo 0)
[ "$CNT" -ge 1 ] && ok "alarma activada ($CNT)" || bad "sin alarmas activas"

echo "== 6) Viewer no puede escribir =="
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/assets" \
  -H "Content-Type: application/json" -d '{"name":"bad"}')
[ "$CODE" = "403" ] && ok "viewer POST /api/assets rechazado (403)" || bad "viewer debio recibir 403, recibio $CODE"

echo "== 7) Discovery sin auth token es 400 =="
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/discovery/tcp-scan" \
  "${H_LAB[@]}" -d '{"host":"127.0.0.1","authorizationToken":"","ports":[80]}')
[ "$CODE" = "400" ] && ok "discovery sin auth = 400" || bad "discovery: esperaba 400, recibio $CODE"

echo "== 8) Backup E2E =="
RESP=$(curl -s -w "\nHTTP:%{http_code}" -X POST "$API_URL/api/admin/backup" "${H_ADMIN[@]}")
CODE=$(echo "$RESP" | grep "^HTTP:" | cut -d: -f2)
BODY=$(echo "$RESP" | grep -v "^HTTP:")
if [ "$CODE" != "200" ]; then
  echo "  backup HTTP $CODE, body:"
  echo "$BODY" | head -5 | sed 's/^/    /'
  bad "backup endpoint devolvio $CODE"
else
  BK_PATH=$(echo "$BODY" | python -c 'import json,sys; print(json.load(sys.stdin)["path"])' 2>&1)
  if [ -z "$BK_PATH" ] || [ ! -f "$BK_PATH" ]; then
    echo "  body recibido: $BODY"
    bad "backup: path invalido o archivo no aparece: '$BK_PATH'"
  else
    ok "backup creado en $BK_PATH"
  fi
fi

echo "== 9) Audit log consultable =="
AUDIT=$(curl -sf "$API_URL/api/admin/audit" -H "X-Atlas-Role: admin")
ACNT=$(echo "$AUDIT" | python -c 'import json,sys; print(json.load(sys.stdin)["count"])' 2>/dev/null || echo 0)
[ "$ACNT" -ge 1 ] && ok "audit log devuelve $ACNT entradas" || bad "audit vacio"

echo "== 9b) Audit chain integra =="
VERIFY=$(curl -sf "$API_URL/api/admin/audit/verify" -H "X-Atlas-Role: admin")
VALID=$(echo "$VERIFY" | python -c 'import json,sys; print(json.load(sys.stdin)["valid"])' 2>/dev/null || echo "false")
[ "$VALID" = "True" ] && ok "audit chain valida" || bad "audit chain invalida: $VERIFY"

echo "== 9c) Export CSV firmado =="
CSV_RESP=$(curl -s -w "\nHTTP:%{http_code}" "$API_URL/api/admin/audit/export.csv" -H "X-Atlas-Role: admin")
CSV_CODE=$(echo "$CSV_RESP" | grep "^HTTP:" | cut -d: -f2)
CSV_BODY=$(echo "$CSV_RESP" | grep -v "^HTTP:")
[ "$CSV_CODE" = "200" ] || { bad "export csv con HTTP $CSV_CODE"; }
if [ "$CSV_CODE" = "200" ]; then
  LAST=$(echo "$CSV_BODY" | tail -1)
  case "$LAST" in
    MANIFEST,*) ok "csv trae MANIFEST firmado" ;;
    *) bad "csv sin MANIFEST: ultima linea: $LAST" ;;
  esac
fi

echo "== 10) Web UI responde HTML para Operador =="
curl -sf "$WEB_URL/" | grep -q "Operador\|Operación\|operador" \
  && ok "Web raiz marca la vista de operador" || bad "Web raiz no tiene marca de rol operador"

echo "== 11) Web UI Admin page responde =="
curl -sf "$WEB_URL/Admin" | grep -qi "Backup\|Device Profiles\|Admin" \
  && ok "Web /Admin existe" || bad "Web /Admin vacia"

echo "== 12) Web UI Device Lab responde =="
curl -sf "$WEB_URL/Lab" | grep -qi "Device Lab\|tripas" \
  && ok "Web /Lab existe" || bad "Web /Lab vacia"

echo
echo "== Resumen prueba reina live =="
echo "  PASS=$PASS  FAIL=$FAIL"
[ "$FAIL" -eq 0 ]

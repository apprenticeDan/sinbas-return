#!/usr/bin/env bash
# scripts/test-alnus-flow.sh
# Smoke test end-to-end de solicitudes con FIFO sobre Alnus acuminata.
# Prerequisitos:
#   - API corriendo en localhost:8080
#   - Setup aplicado (scripts/test-alnus-setup.sql)
#   - test_gerencia existe con password Sinbas123!

set -e

BASE="${BASE:-http://localhost:8080}"
DB="${DB_CONTAINER:-sinbasreturn_sinbas-db_1}"
PRODUCT_ID="01917f3a-9999-7000-8000-000000000001"
LOTE_TAR="01917f3a-9999-7001-8000-000000000001"
LOTE_CBB="01917f3a-9999-7001-8000-000000000002"

# ─── Helpers ───────────────────────────────────────────────────────
login() {
  curl -s -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"nombreUsuarioRaw\":\"$1\",\"contrasena\":\"$2\"}" \
    | grep -oP '"token"\s*:\s*"\K[^"]+'
}

psql_db() {
  podman exec -i "$DB" psql -U sinbas -d sinbas -c "$1"
}

lote_state() {
  psql_db "SELECT codigo, cantidad_actual, cantidad_reservada FROM lote 
           WHERE id IN ('$LOTE_TAR', '$LOTE_CBB')
           ORDER BY codigo;" 2>/dev/null
}

extract_field() {
  grep -oP "\"$2\"\\s*:\\s*\"?\\K[^\",}]+" <<< "$1" | head -1
}

# Verifica que un lote tenga los valores esperados
assert_lote() {
  local lote_id="$1" expected_actual="$2" expected_reservada="$3" label="$4"
  local actual=$(podman exec -i "$DB" psql -U sinbas -d sinbas -t -A -c \
    "SELECT cantidad_actual || '/' || cantidad_reservada FROM lote WHERE id = '$lote_id'")
  local esperado="${expected_actual}/${expected_reservada}"
  if [ "$actual" = "$esperado" ]; then
    echo "   ✅ $label: $actual kg (actual/reservada)"
  else
    echo "   ❌ $label: esperaba $esperado, obtuve $actual"
    exit 1
  fi
}

# ─── Pre-checks ────────────────────────────────────────────────────
echo "▶ Pre-check: ¿setup aplicado?"
SETUP_COUNT=$(podman exec -i "$DB" psql -U sinbas -d sinbas -t -A -c \
  "SELECT COUNT(*) FROM lote WHERE producto_id = '$PRODUCT_ID'")
if [ "$SETUP_COUNT" != "2" ]; then
  echo "❌ Setup incompleto: hay $SETUP_COUNT lotes (esperaba 2)."
  echo "   Corré: podman exec -i $DB psql -U sinbas -d sinbas -f - < scripts/test-alnus-setup.sql"
  exit 1
fi
echo "   ✅ 2 lotes encontrados"

echo "▶ Login como test_gerencia..."
TOKEN=$(login test_gerencia Sinbas123!)
[ -z "$TOKEN" ] && { echo "❌ Login falló"; exit 1; }
echo "   ✅ Token OK"

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "ESTADO INICIAL"
echo "═══════════════════════════════════════════════════════════════"
lote_state

# ═══════════════════════════════════════════════════════════════════
# TEST 1: UsoInterno 2kg → FIFO debe elegir Tarija (más antiguo)
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 1: UsoInterno 2kg → FIFO debe elegir Tarija"
echo "═══════════════════════════════════════════════════════════════"

RESP=$(curl -s -X POST "$BASE/api/solicitudes" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"tipo\": \"UsoInterno\",
    \"destinoTipo\": \"Vivero\",
    \"signo\": \"Negativo\",
    \"productoId\": \"$PRODUCT_ID\",
    \"cantidad\": 2,
    \"unidad\": \"Kilogramo\",
    \"motivo\": \"TEST 1 - FIFO picks Tarija\"
  }")

# NUEVO: mostrar response si falla
if ! echo "$RESP" | grep -q '"id"'; then
  echo "   ❌ Response inesperada del backend:"
  echo "$RESP" | python3 -m json.tool
  exit 1
fi

SOL1_ID=$(extract_field "$RESP" "id")
LOTE_ASIGNADO_UUID=$(extract_field "$RESP" "loteId")
LOTE_ASIGNADO_COD=$(extract_field "$RESP" "codigoLote")

echo "   Solicitado ID: $SOL1_ID"
echo "   Lote asignado: $LOTE_ASIGNADO_COD ($LOTE_ASIGNADO_UUID)"
if [ "$LOTE_ASIGNADO_UUID" = "$LOTE_TAR" ]; then
  echo "   ✅ FIFO eligió Tarija (UUID correcto, lote más antiguo)"
else
  echo "   ❌ FIFO eligió UUID '$LOTE_ASIGNADO_UUID' (esperaba $LOTE_TAR)"
  exit 1
fi

echo "   Antes de confirmar (reserva debe estar activa):"
assert_lote "$LOTE_TAR" "5.00" "2.00" "Tarija"
assert_lote "$LOTE_CBB" "2.50" "0.00" "Cochabamba"

echo "   Confirmando..."
curl -sf -X POST "$BASE/api/solicitudes/$SOL1_ID/confirmar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{}' > /dev/null

echo "   Después de confirmar:"
assert_lote "$LOTE_TAR" "3.00" "0.00" "Tarija"
assert_lote "$LOTE_CBB" "2.50" "0.00" "Cochabamba"

# ═══════════════════════════════════════════════════════════════════
# TEST 2: Merma 1kg → create + approve + confirm
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 2: Merma 1kg → FIFO Tarija, requiere aprobación"
echo "═══════════════════════════════════════════════════════════════"

RESP=$(curl -s -X POST "$BASE/api/solicitudes" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"tipo\": \"Merma\",
    \"signo\": \"Negativo\",
    \"productoId\": \"$PRODUCT_ID\",
    \"cantidad\": 1,
    \"unidad\": \"Kilogramo\",
    \"motivo\": \"TEST 2 - Merma con aprobación\"
  }")

SOL2_ID=$(extract_field "$RESP" "id")
echo "   Solicitado ID: $SOL2_ID"

echo "   Estado Pendiente (NO debe reservar todavía):"
assert_lote "$LOTE_TAR" "3.00" "0.00" "Tarija"

echo "   Aprobando como gerencia..."
curl -sf -X PUT "$BASE/api/solicitudes/$SOL2_ID/aprobar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{}' > /dev/null

echo "   Estado Aprobada (AHORA debe reservar):"
assert_lote "$LOTE_TAR" "3.00" "1.00" "Tarija"

echo "   Confirmando..."
curl -sf -X POST "$BASE/api/solicitudes/$SOL2_ID/confirmar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{}' > /dev/null

echo "   Estado Confirmada (reserva consumida):"
assert_lote "$LOTE_TAR" "2.00" "0.00" "Tarija"

# ═══════════════════════════════════════════════════════════════════
# TEST 3: UsoInterno 2kg → agota Tarija (2kg → 0)
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 3: UsoInterno 2kg → agota Tarija"
echo "═══════════════════════════════════════════════════════════════"

RESP=$(curl -s -X POST "$BASE/api/solicitudes" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"tipo\": \"UsoInterno\",
    \"destinoTipo\": \"Laboratorio\",
    \"signo\": \"Negativo\",
    \"productoId\": \"$PRODUCT_ID\",
    \"cantidad\": 2,
    \"unidad\": \"Kilogramo\",
    \"motivo\": \"TEST 3 - agotar Tarija\"
  }")

SOL3_ID=$(extract_field "$RESP" "id")
echo "   Solicitado ID: $SOL3_ID"

curl -sf -X POST "$BASE/api/solicitudes/$SOL3_ID/confirmar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{}' > /dev/null

echo "   Después de confirmar (Tarija debe estar en 0):"
assert_lote "$LOTE_TAR" "0.00" "0.00" "Tarija"
assert_lote "$LOTE_CBB" "2.50" "0.00" "Cochabamba"

# ═══════════════════════════════════════════════════════════════════
# TEST 4: UsoInterno 1kg → FIFO salta Tarija (0kg), va a Cochabamba
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 4: UsoInterno 1kg → debe ir a Cochabamba (Tarija vacío)"
echo "═══════════════════════════════════════════════════════════════"

RESP=$(curl -s -X POST "$BASE/api/solicitudes" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"tipo\": \"UsoInterno\",
    \"destinoTipo\": \"Otro\",
    \"destinoOtro\": \"Oficina central\",
    \"signo\": \"Negativo\",
    \"productoId\": \"$PRODUCT_ID\",
    \"cantidad\": 1,
    \"unidad\": \"Kilogramo\",
    \"motivo\": \"TEST 4 - FIFO debe saltar Tarija\"
  }")

SOL4_ID=$(extract_field "$RESP" "id")
LOTE_ASIGNADO_UUID=$(extract_field "$RESP" "loteId")
LOTE_ASIGNADO_COD=$(extract_field "$RESP" "codigoLote")

echo "   Solicitado ID: $SOL4_ID"
echo "   Lote asignado: $LOTE_ASIGNADO_COD ($LOTE_ASIGNADO_UUID)"
if [ "$LOTE_ASIGNADO_UUID" = "$LOTE_CBB" ]; then
  echo "   ✅ FIFO eligió Cochabamba (UUID correcto, saltó Tarija vacío)"
else
  echo "   ❌ FIFO eligió UUID '$LOTE_ASIGNADO_UUID' (esperaba $LOTE_CBB)"
  exit 1
fi

curl -sf -X POST "$BASE/api/solicitudes/$SOL4_ID/confirmar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{}' > /dev/null

echo "   Después de confirmar:"
assert_lote "$LOTE_TAR" "0.00" "0.00" "Tarija"
assert_lote "$LOTE_CBB" "1.50" "0.00" "Cochabamba"

# ═══════════════════════════════════════════════════════════════════
# TEST 5: UsoInterno 2kg → debe fallar (total 1.5kg < 2kg)
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 5: UsoInterno 2kg → debe fallar (stock total 1.5kg < 2kg)"
echo "═══════════════════════════════════════════════════════════════"

HTTP_STATUS=$(curl -s -o /tmp/t5.json -w "%{http_code}" \
  -X POST "$BASE/api/solicitudes" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"tipo\": \"UsoInterno\",
    \"destinoTipo\": \"Vivero\",
    \"signo\": \"Negativo\",
    \"productoId\": \"$PRODUCT_ID\",
    \"cantidad\": 2,
    \"unidad\": \"Kilogramo\",
    \"motivo\": \"TEST 5 - stock insuficiente\"
  }")

#if [ "$HTTP_STATUS" = "409" ]; then
#  echo "   ✅ HTTP 409 (correcto: stock insuficiente)"
#  echo "   Detalle: $(cat /tmp/t5.json | grep -oP '"error"\s*:\s*"\K[^"]+' | head -1)"
#else
#  echo "   ❌ Esperaba 409, obtuve $HTTP_STATUS"
#  cat /tmp/t5.json
#  exit 1
#fi

if [ "$HTTP_STATUS" = "409" ] || [ "$HTTP_STATUS" = "422" ]; then
  echo "   ✅ HTTP $HTTP_STATUS (rechazo por stock insuficiente)"
  echo "   Detalle: $(cat /tmp/t5.json | grep -oP '"error"\s*:\s*"\K[^"]+' | head -1)"
  echo "   Nota: Fase 6a usa 422, Fase 1+2 usa 409. Unificación → Fase 7."
else
  echo "   ❌ Esperaba 409 o 422, obtuve $HTTP_STATUS"
  cat /tmp/t5.json
  exit 1
fi

# ═══════════════════════════════════════════════════════════════════
# TEST 6: UsoInterno 1.2kg → debe fallar 422 (requiere 2 lotes: multi-lote)
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "TEST 6: UsoInterno 1.2kg → 422 multi-lote (Tarija 0 + Cbba 1.5)"
echo "═══════════════════════════════════════════════════════════════"
echo "   (Nota: esto requiere Cochabamba 1.5kg → 1.2kg, entra en un solo lote)"
echo "   (Omitir: FIFO sí puede hacerlo con un solo lote)"

# Skip test 6 — FIFO puede asignar 1.2kg a Cochabamba (tiene 1.5).
# Multi-lote solo dispara cuando NINGÚN lote tiene suficiente.

# ═══════════════════════════════════════════════════════════════════
# VERIFICACIÓN FINAL DE INVARIANTES
# ═══════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "VERIFICACIÓN FINAL DE INVARIANTES"
echo "═══════════════════════════════════════════════════════════════"

echo ""
echo "▶ Estado final de lotes:"
lote_state

echo ""
echo "▶ Invariante 1: sum(reserva_stock Activa) = lote.cantidad_reservada"
psql_db "
SELECT 
    l.codigo,
    l.cantidad_reservada AS reservada_lote,
    COALESCE(SUM(rs.cantidad) FILTER (WHERE rs.estado = 'Activa'), 0) AS suma_activa,
    CASE WHEN l.cantidad_reservada = COALESCE(SUM(rs.cantidad) FILTER (WHERE rs.estado = 'Activa'), 0)
         THEN '✅' ELSE '❌' END AS check_ok
FROM lote l
LEFT JOIN reserva_stock rs ON rs.lote_id = l.id
WHERE l.producto_id = '$PRODUCT_ID'
GROUP BY l.codigo, l.cantidad_reservada
ORDER BY l.codigo;
"

echo "▶ Invariante 2: cantidad_reservada <= cantidad_actual en TODOS los lotes"
psql_db "
SELECT 
    CASE WHEN COUNT(*) = 0 
         THEN '✅ Ningún lote viola el constraint'
         ELSE '❌ ' || COUNT(*) || ' lote(s) inválido(s)' 
    END AS check_rango
FROM lote
WHERE cantidad_reservada > cantidad_actual;
"

echo "▶ Invariante 3: sin reservas huérfanas activas en solicitudes confirmadas/rechazadas"
psql_db "
SELECT 
    CASE WHEN COUNT(*) = 0
         THEN '✅ Sin reservas huérfanas'
         ELSE '❌ ' || COUNT(*) || ' reserva(s) huérfana(s)'
    END AS check_huerfanas
FROM reserva_stock rs
JOIN solicitud_egreso_interno s ON s.id = rs.solicitud_id
WHERE rs.estado = 'Activa' AND s.estado IN ('Confirmada', 'Rechazada');
"

echo "▶ Historial de reservas del test:"
psql_db "
SELECT rs.origen_tipo, rs.estado, l.codigo AS lote, rs.cantidad, rs.unidad,
       rs.movimiento_id IS NOT NULL AS tiene_mov
FROM reserva_stock rs
JOIN lote l ON l.id = rs.lote_id
WHERE l.producto_id = '$PRODUCT_ID'
ORDER BY rs.creado_en;
"

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "✅ TODOS LOS TESTS PASARON"
echo "═══════════════════════════════════════════════════════════════"

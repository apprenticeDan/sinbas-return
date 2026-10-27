-- scripts/test-alnus-verify.sql
-- Verificación completa del estado post-tests.
-- Se puede correr en cualquier momento (no modifica datos).

\echo '=== 1. Estado de los 2 lotes ==='
SELECT codigo, procedencia, cantidad_actual, cantidad_reservada,
       (cantidad_actual - cantidad_reservada) AS disponible,
       unidad, fecha_ingreso, estado
FROM lote 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001'
ORDER BY fecha_ingreso;

\echo ''
\echo '=== 2. Totales del producto ==='
SELECT 
    SUM(cantidad_actual) AS total_kg,
    SUM(cantidad_reservada) AS reservado_kg,
    SUM(cantidad_actual - cantidad_reservada) AS disponible_kg
FROM lote 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001';

\echo ''
\echo '=== 3. Consistencia reserva_stock ↔ lote.cantidad_reservada ==='
SELECT 
    l.codigo,
    l.cantidad_reservada AS reservada_en_lote,
    COALESCE(SUM(rs.cantidad) FILTER (WHERE rs.estado = 'Activa'), 0) AS suma_reservas_activas,
    CASE WHEN l.cantidad_reservada = COALESCE(SUM(rs.cantidad) FILTER (WHERE rs.estado = 'Activa'), 0)
         THEN '✅' ELSE '❌ INCONSISTENTE' END AS check_ok
FROM lote l
LEFT JOIN reserva_stock rs ON rs.lote_id = l.id
WHERE l.producto_id = '01917f3a-9999-7000-8000-000000000001'
GROUP BY l.codigo, l.cantidad_reservada
ORDER BY l.codigo;

\echo ''
\echo '=== 4. Historial completo de reservas ==='
SELECT 
    rs.origen_tipo, rs.estado, 
    l.codigo AS lote, 
    rs.cantidad, rs.unidad,
    rs.creado_en,
    rs.despachado_en,
    rs.movimiento_id IS NOT NULL AS tiene_mov
FROM reserva_stock rs
JOIN lote l ON l.id = rs.lote_id
WHERE l.producto_id = '01917f3a-9999-7000-8000-000000000001'
ORDER BY rs.creado_en;

\echo ''
\echo '=== 5. Historial de solicitudes ==='
SELECT 
    tipo, signo, estado, cantidad, unidad,
    lote_id,
    CASE WHEN resolucion_aprobador_id IS NOT NULL THEN 'Sí' ELSE 'No' END AS aprobada,
    CASE WHEN entregado_por IS NOT NULL THEN 'Sí' ELSE 'No' END AS confirmada,
    motivo,
    creado_en
FROM solicitud_egreso_interno
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001'
ORDER BY creado_en;

\echo ''
\echo '=== 6. Movimientos de inventario generados ==='
SELECT 
    mi.tipo, mi.motivo, mi.fecha,
    lm.cantidad, lm.unidad,
    l.codigo AS lote
FROM movimiento_inventario mi
JOIN linea_movimiento lm ON lm.movimiento_id = mi.id
JOIN lote l ON l.id = lm.lote_id
WHERE l.producto_id = '01917f3a-9999-7000-8000-000000000001'
ORDER BY mi.fecha;

\echo ''
\echo '=== 7. CHECK invariantes globales ==='
SELECT 
    'Rango reservada <= actual' AS check_name,
    CASE WHEN COUNT(*) = 0 THEN '✅ OK' ELSE '❌ ' || COUNT(*) || ' violaciones' END AS resultado
FROM lote WHERE cantidad_reservada > cantidad_actual OR cantidad_reservada < 0
UNION ALL
SELECT 
    'Sin reservas huérfanas activas',
    CASE WHEN COUNT(*) = 0 THEN '✅ OK' ELSE '❌ ' || COUNT(*) || ' huérfanas' END
FROM reserva_stock rs
JOIN solicitud_egreso_interno s ON s.id = rs.solicitud_id
WHERE rs.estado = 'Activa' AND s.estado IN ('Confirmada', 'Rechazada');

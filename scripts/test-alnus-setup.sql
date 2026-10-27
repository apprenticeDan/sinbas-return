-- scripts/test-alnus-setup.sql
-- Setup idempotente para smoke test de FIFO con producto Alnus acuminata.
-- Producto UUID: 01917f3a-9999-7000-8000-000000000001
-- Lote Tarija:   01917f3a-9999-7001-8000-000000000001 (5kg, 2026-10-01)
-- Lote Cbba:     01917f3a-9999-7001-8000-000000000002 (2.5kg, 2026-10-20)

-- ─── Cleanup de ejecuciones previas ────────────────────────────────
-- Orden: hijo → padre (respetar FKs)
DELETE FROM reserva_stock 
WHERE lote_id IN (
    SELECT id FROM lote WHERE producto_id = '01917f3a-9999-7000-8000-000000000001'
);

DELETE FROM linea_movimiento 
WHERE lote_id IN (
    SELECT id FROM lote WHERE producto_id = '01917f3a-9999-7000-8000-000000000001'
);

DELETE FROM solicitud_egreso_interno 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001';

DELETE FROM lote 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001';

DELETE FROM producto 
WHERE id = '01917f3a-9999-7000-8000-000000000001';

-- Nota: movimiento_inventario huérfanos quedan (lineas_movimiento ya borradas).
-- En entorno de test es aceptable. No rompe constraints.

-- ─── Insert producto ───────────────────────────────────────────────
INSERT INTO producto (
    id, categoria, genero, epiteto, observaciones_nc, nombres_comunes,
    etapa_desarrollo, nombre_insumo, marca_insumo, descripcion_insumo,
    empaque, unidad_manejo, gramos_nominales, trazabilidad,
    precio_oficial, precio_moneda, precio_fecha, precio_usuario_id,
    estado_comercial, activo, observaciones
) VALUES (
    '01917f3a-9999-7000-8000-000000000001',
    'Semilla',
    'Alnus',
    'acuminata',
    NULL,
    '["Aliso"]',
    NULL, NULL, NULL, NULL,
    'Bolsa',
    'Kilogramo',
    1000,
    'PorLote',
    50.00,
    'BOB',
    NOW(),
    NULL,
    'ActivoParaVenta',
    true,
    'TEST SETUP - dos lotes FIFO (Tarija viejo, Cochabamba nuevo)'
);

-- ─── Insert 2 lotes ────────────────────────────────────────────────
INSERT INTO lote (
    id, codigo, producto_id, procedencia,
    cantidad_inicial, cantidad_actual, cantidad_reservada,
    unidad, fecha_ingreso, ubicacion, estado, observaciones
) VALUES 
(
    '01917f3a-9999-7001-8000-000000000001',
    'ALNUSACU-02610-01',
    '01917f3a-9999-7000-8000-000000000001',
    'Tarija - Reserva Sama',
    5.00, 5.00, 0.00,
    'Kilogramo', '2026-10-01', 'Almacén Central - Estante A2',
    'Activo', 'TEST FIFO - TARIJA lote más antiguo (2026-10-01)'
),
(
    '01917f3a-9999-7001-8000-000000000002',
    'ALNUSACU-02610-002',
    '01917f3a-9999-7000-8000-000000000001',
    'Cochabamba - Vivero Municipal',
    2.50, 2.50, 0.00,
    'Kilogramo', '2026-10-20', 'Almacén Central - Estante A3',
    'Activo', 'TEST FIFO - COCHA lote más reciente (2026-10-20)'
);

-- ─── Verificación del setup ────────────────────────────────────────
\echo ''
\echo '=== Setup completado. Estado inicial: ==='
SELECT codigo, procedencia, cantidad_actual, cantidad_reservada, unidad, fecha_ingreso
FROM lote 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001'
ORDER BY fecha_ingreso;

\echo ''
\echo 'Total stock:'
SELECT SUM(cantidad_actual) AS total_kg,
       SUM(cantidad_reservada) AS reservado_kg,
       SUM(cantidad_actual - cantidad_reservada) AS disponible_kg
FROM lote 
WHERE producto_id = '01917f3a-9999-7000-8000-000000000001';

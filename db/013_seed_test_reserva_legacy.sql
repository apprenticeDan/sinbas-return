-- ─────────────────────────────────────────────────────────────
-- Migración 013: Seed de prueba para reserva legacy (Fase 4)
--
-- IMPORTANTE: este script es idempotente. Se puede correr múltiples veces.
-- Usa ON CONFLICT DO NOTHING en cada INSERT para no fallar si el reset no se hizo.
-- En el flujo normal (reset + carga), corre una sola vez y crea el caso testigo desde cero.
-- ─────────────────────────────────────────────────────────────

-- 1. Empleado y Usuario responsable (si no existen)
INSERT INTO empleado (id, nombre_completo, estado)
VALUES ('01917f3a-0001-7000-8000-000000000001', 'Administrador del Sistema', 'Activo')
ON CONFLICT (id) DO NOTHING;

INSERT INTO usuario (id, empleado_id, nombre_usuario, password_hash, estado)
VALUES (
    '01917f3a-0002-7000-8000-000000000002',
    '01917f3a-0001-7000-8000-000000000001',
    'admin',
    '$2a$11$8bv8pfyb92XqSnbzqIP9vuvXcnfCotYVp6Svj6ASNbYJTftltLBmu',
    'Activo'
)
ON CONFLICT (id) DO NOTHING;

-- 2. Cliente de prueba
INSERT INTO cliente (
    id, tipo, nombres, apellido_paterno, apellido_materno, ci_numero, ci_extension,
    telefono, email, direccion, estado
)
VALUES (
    '01917f3a-0006-7000-8000-000000000001',
    'Natural',
    'Carlos',
    'Mendoza',
    'Vaca',
    '4892011',
    'CB',
    '+591 76543210',
    'carlos.mendoza@vivero.bo',
    'Av. Blanco Galindo Km 5, Cochabamba',
    'Activo'
)
ON CONFLICT (id) DO NOTHING;

-- 3. Producto: Semilla de Mara / Caoba (unidad_manejo = Kilogramo, ActivoParaVenta)
INSERT INTO producto (
    id, categoria, genero, epiteto, nombres_comunes,
    unidad_manejo, trazabilidad, precio_oficial, precio_moneda, precio_fecha,
    estado_comercial, activo, observaciones
)
VALUES (
    '01917f3a-0003-7000-8000-000000000001',
    'Semilla',
    'Swietenia',
    'macrophylla',
    'Caoba, Mara',
    'Kilogramo',
    'PorLote',
    150.00,
    'BOB',
    NOW(),
    'ActivoParaVenta',
    TRUE,
    'Semilla forestal de alta calidad'
)
ON CONFLICT (id) DO NOTHING;

-- 4. Lote: SWIETMAC-02608-01 (unidad = Gramo, 5000g actual, 1000g reservada)
INSERT INTO lote (
    id, codigo, producto_id, procedencia, cantidad_inicial, cantidad_actual,
    cantidad_reservada, unidad, fecha_ingreso, ubicacion, estado, observaciones
)
VALUES (
    '01917f3a-0004-7000-8000-000000000001',
    'SWIETMAC-02608-01',
    '01917f3a-0003-7000-8000-000000000001',
    'Bosque Chiquitano - Reserva Don Mario',
    5000.00,
    5000.00,
    1000.00,
    'Gramo',
    '2026-08-15',
    'Almacén Central - Estante A1',
    'Activo',
    'Semilla recolectada en temporada alta con buena viabilidad'
)
ON CONFLICT (id) DO NOTHING;

-- 5. Proforma previa para satisfacer FK de orden_venta
INSERT INTO proforma (
    id, fecha, responsable_id, cliente_id, estado, total, moneda
)
VALUES (
    '01917f3a-0010-7000-8000-000000000001',
    CURRENT_DATE,
    '01917f3a-0002-7000-8000-000000000002',
    '01917f3a-0006-7000-8000-000000000001',
    'Convertida',
    150.00,
    'BOB'
)
ON CONFLICT (id) DO NOTHING;

-- 6. Orden de Venta Confirmada
INSERT INTO orden_venta (
    id, codigo, proforma_id, cliente_id, fecha, responsable_id, total, moneda, estado
)
VALUES (
    '01917f3a-0011-7000-8000-000000000001',
    'OV-TEST-001',
    '01917f3a-0010-7000-8000-000000000001',
    '01917f3a-0006-7000-8000-000000000001',
    CURRENT_DATE,
    '01917f3a-0001-7000-8000-000000000001',
    150.00,
    'BOB',
    'Confirmada'
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO linea_orden_venta (
    orden_venta_id, item, producto_id, cantidad, unidad, precio_unitario, subtotal
)
VALUES (
    '01917f3a-0011-7000-8000-000000000001',
    1,
    '01917f3a-0003-7000-8000-000000000001',
    1.0000,
    'Kilogramo',
    150.00,
    150.00
)
ON CONFLICT (orden_venta_id, item) DO NOTHING;

-- 7. Orden de Despacho Pendiente asociada a la venta
INSERT INTO orden_despacho (
    id, codigo, orden_venta_id, origen_tipo, cliente_id, estado
)
VALUES (
    '01917f3a-0011-7000-8000-000000000002',
    'OD-TEST-001',
    '01917f3a-0011-7000-8000-000000000001',
    'DeVenta',
    '01917f3a-0006-7000-8000-000000000001',
    'Pendiente'
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO linea_despacho (
    orden_despacho_id, item, producto_id, cantidad, unidad
)
VALUES (
    '01917f3a-0011-7000-8000-000000000002',
    1,
    '01917f3a-0003-7000-8000-000000000001',
    1.0000,
    'Kilogramo'
)
ON CONFLICT (orden_despacho_id, item) DO NOTHING;

-- 8. Fila en reserva_stock: Activa, 1000g, asociada a la venta y lote
INSERT INTO reserva_stock (
    id, origen_tipo, orden_venta_id, solicitud_id, lote_id, cantidad, unidad, estado, movimiento_id
)
VALUES (
    '01917f3a-0012-7000-8000-000000000001',
    'VENTA',
    '01917f3a-0011-7000-8000-000000000001',
    NULL,
    '01917f3a-0004-7000-8000-000000000001',
    1000.0000,
    'Gramo',
    'Activa',
    NULL
)
ON CONFLICT (id) DO NOTHING;

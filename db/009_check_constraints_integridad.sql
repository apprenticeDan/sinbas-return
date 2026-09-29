-- Migración 009: Check Constraints de Integridad de Datos (Bloque E)
-- Fecha: 2026-09-29
-- Descripción:
--   Blindaje a nivel de base de datos relacional para evitar la persistencia
--   de datos corruptos que violen invariantes físicas o de negocio.

-- 1. Tabla producto: contenido nominal positivo, precio no negativo, categorías y estados válidos
ALTER TABLE producto
    DROP CONSTRAINT IF EXISTS chk_producto_gramos_nominales_positivo,
    DROP CONSTRAINT IF EXISTS chk_producto_precio_no_negativo,
    DROP CONSTRAINT IF EXISTS chk_producto_categoria_valida,
    DROP CONSTRAINT IF EXISTS chk_producto_estado_comercial_valido;

ALTER TABLE producto
    ADD CONSTRAINT chk_producto_gramos_nominales_positivo
        CHECK (gramos_nominales IS NULL OR gramos_nominales > 0),
    ADD CONSTRAINT chk_producto_precio_no_negativo
        CHECK (precio_oficial IS NULL OR precio_oficial >= 0),
    ADD CONSTRAINT chk_producto_categoria_valida
        CHECK (categoria IN ('Semilla', 'Plantin', 'Insumo')),
    ADD CONSTRAINT chk_producto_estado_comercial_valido
        CHECK (estado_comercial IN ('PendientePrecioBorrador', 'ActivoParaVenta', 'Inactivo'));

-- 2. Tabla lote: cantidades no negativas, estado válido
ALTER TABLE lote
    DROP CONSTRAINT IF EXISTS chk_lote_cantidad_inicial_positiva,
    DROP CONSTRAINT IF EXISTS chk_lote_cantidad_actual_no_negativa,
    DROP CONSTRAINT IF EXISTS chk_lote_estado_valido;

ALTER TABLE lote
    ADD CONSTRAINT chk_lote_cantidad_inicial_positiva
        CHECK (cantidad_inicial > 0),
    ADD CONSTRAINT chk_lote_cantidad_actual_no_negativa
        CHECK (cantidad_actual >= 0),
    ADD CONSTRAINT chk_lote_estado_valido
        CHECK (estado IN ('Activo', 'EnCuarentena', 'Rechazado', 'Agotado', 'Bloqueado', 'Archivado'));

-- 3. Tabla analisis_laboratorio: porcentajes entre 0 y 100
ALTER TABLE analisis_laboratorio
    DROP CONSTRAINT IF EXISTS chk_analisis_germinacion_rango,
    DROP CONSTRAINT IF EXISTS chk_analisis_pureza_rango,
    DROP CONSTRAINT IF EXISTS chk_analisis_humedad_rango,
    DROP CONSTRAINT IF EXISTS chk_analisis_viabilidad_rango;

ALTER TABLE analisis_laboratorio
    ADD CONSTRAINT chk_analisis_germinacion_rango
        CHECK (germinacion >= 0 AND germinacion <= 100),
    ADD CONSTRAINT chk_analisis_pureza_rango
        CHECK (pureza >= 0 AND pureza <= 100),
    ADD CONSTRAINT chk_analisis_humedad_rango
        CHECK (humedad >= 0 AND humedad <= 100),
    ADD CONSTRAINT chk_analisis_viabilidad_rango
        CHECK (viabilidad >= 0 AND viabilidad <= 100);

-- 4. Tabla movimiento_inventario: tipo válido
ALTER TABLE movimiento_inventario
    DROP CONSTRAINT IF EXISTS chk_movimiento_tipo_valido;

ALTER TABLE movimiento_inventario
    ADD CONSTRAINT chk_movimiento_tipo_valido
        CHECK (tipo IN ('Entrada', 'Salida'));

-- 5. Tabla linea_movimiento: cantidad estrictamente positiva
ALTER TABLE linea_movimiento
    DROP CONSTRAINT IF EXISTS chk_linea_movimiento_cantidad_positiva;

ALTER TABLE linea_movimiento
    ADD CONSTRAINT chk_linea_movimiento_cantidad_positiva
        CHECK (cantidad > 0);

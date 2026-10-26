-- ─────────────────────────────────────────────────────────────
-- Migración 012: Tabla reserva_stock (Fase 4)
-- ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS reserva_stock (
    id                  UUID PRIMARY KEY,
    origen_tipo         VARCHAR(20) NOT NULL CHECK (origen_tipo IN ('VENTA', 'SOLICITUD')),
    orden_venta_id      UUID NULL REFERENCES orden_venta(id),
    solicitud_id        UUID NULL, -- FK cuando exista tabla solicitud_egreso_interno
    lote_id             UUID NOT NULL REFERENCES lote(id),
    cantidad            NUMERIC(12,4) NOT NULL CHECK (cantidad > 0),
    unidad              VARCHAR(30) NOT NULL,
    estado              VARCHAR(20) NOT NULL DEFAULT 'Activa' CHECK (estado IN ('Activa', 'Liberada', 'Despachada')),
    movimiento_id       UUID NULL REFERENCES movimiento_inventario(id),
    creado_en           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    liberado_en         TIMESTAMPTZ NULL,
    despachado_en       TIMESTAMPTZ NULL,

    CONSTRAINT chk_reserva_origen CHECK (
        (origen_tipo = 'VENTA' AND orden_venta_id IS NOT NULL AND solicitud_id IS NULL) OR
        (origen_tipo = 'SOLICITUD' AND solicitud_id IS NOT NULL AND orden_venta_id IS NULL)
    ),

    CONSTRAINT chk_reserva_timestamps CHECK (
        (estado = 'Activa' AND liberado_en IS NULL AND despachado_en IS NULL AND movimiento_id IS NULL) OR
        (estado = 'Liberada' AND liberado_en IS NOT NULL AND despachado_en IS NULL AND movimiento_id IS NULL) OR
        (estado = 'Despachada' AND despachado_en IS NOT NULL AND movimiento_id IS NOT NULL AND liberado_en IS NULL)
    )
);

CREATE INDEX IF NOT EXISTS idx_reserva_stock_ov_activa ON reserva_stock(orden_venta_id) WHERE estado = 'Activa' AND origen_tipo = 'VENTA';
CREATE INDEX IF NOT EXISTS idx_reserva_stock_lote_activa ON reserva_stock(lote_id) WHERE estado = 'Activa';

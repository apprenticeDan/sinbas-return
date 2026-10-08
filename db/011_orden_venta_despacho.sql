-- ─────────────────────────────────────────────────────────────
-- Feature F8: Confirmación de Venta y Despacho Físico (011_orden_venta_despacho.sql)
-- ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS orden_venta (
    id                  UUID PRIMARY KEY,
    codigo              VARCHAR(50) NOT NULL UNIQUE,
    proforma_id         UUID NOT NULL UNIQUE REFERENCES proforma(id),
    cliente_id          UUID NOT NULL REFERENCES cliente(id),
    fecha               DATE NOT NULL DEFAULT CURRENT_DATE,
    responsable_id      UUID NOT NULL REFERENCES empleado(id),
    total               NUMERIC(14,2) NOT NULL CHECK (total >= 0),
    moneda              VARCHAR(10) NOT NULL DEFAULT 'BOB',
    estado              VARCHAR(30) NOT NULL DEFAULT 'Confirmada',
    creado_en           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    anulacion_motivo    TEXT NULL,
    anulado_por         UUID NULL REFERENCES empleado(id),
    anulado_en          TIMESTAMPTZ NULL,
    CONSTRAINT ck_orden_venta_estado CHECK (estado IN ('Confirmada', 'Despachada', 'Anulada'))
);

CREATE INDEX IF NOT EXISTS ix_orden_venta_cliente ON orden_venta(cliente_id);
CREATE INDEX IF NOT EXISTS ix_orden_venta_estado ON orden_venta(estado);
CREATE INDEX IF NOT EXISTS ix_orden_venta_fecha ON orden_venta(fecha);
CREATE INDEX IF NOT EXISTS ix_orden_venta_proforma ON orden_venta(proforma_id);

CREATE TABLE IF NOT EXISTS linea_orden_venta (
    orden_venta_id      UUID NOT NULL REFERENCES orden_venta(id) ON DELETE CASCADE,
    item                INTEGER NOT NULL,
    producto_id         UUID NOT NULL REFERENCES producto(id),
    cantidad            NUMERIC(14,4) NOT NULL CHECK (cantidad > 0),
    unidad              VARCHAR(20) NOT NULL,
    precio_unitario     NUMERIC(14,2) NOT NULL CHECK (precio_unitario > 0),
    subtotal            NUMERIC(14,2) NOT NULL CHECK (subtotal >= 0),
    PRIMARY KEY (orden_venta_id, item)
);

CREATE INDEX IF NOT EXISTS ix_linea_orden_venta_producto ON linea_orden_venta(producto_id);

CREATE TABLE IF NOT EXISTS orden_despacho (
    id                  UUID PRIMARY KEY,
    codigo              VARCHAR(50) NOT NULL UNIQUE,
    orden_venta_id      UUID UNIQUE REFERENCES orden_venta(id),
    origen_tipo         VARCHAR(30) NOT NULL DEFAULT 'DeVenta',
    cliente_id          UUID REFERENCES cliente(id),
    estado              VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    movimiento_id       UUID NULL REFERENCES movimiento_inventario(id),
    creado_en           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    anulacion_motivo    TEXT NULL,
    anulado_por         UUID NULL REFERENCES empleado(id),
    anulado_en          TIMESTAMPTZ NULL,
    CONSTRAINT ck_orden_despacho_estado CHECK (estado IN ('Pendiente', 'Despachado', 'Anulado'))
);

CREATE INDEX IF NOT EXISTS ix_orden_despacho_estado ON orden_despacho(estado);
CREATE INDEX IF NOT EXISTS ix_orden_despacho_venta ON orden_despacho(orden_venta_id);

CREATE TABLE IF NOT EXISTS linea_despacho (
    orden_despacho_id   UUID NOT NULL REFERENCES orden_despacho(id) ON DELETE CASCADE,
    item                INTEGER NOT NULL,
    producto_id         UUID NOT NULL REFERENCES producto(id),
    cantidad            NUMERIC(14,4) NOT NULL CHECK (cantidad > 0),
    unidad              VARCHAR(20) NOT NULL,
    PRIMARY KEY (orden_despacho_id, item)
);

CREATE INDEX IF NOT EXISTS ix_linea_despacho_producto ON linea_despacho(producto_id);

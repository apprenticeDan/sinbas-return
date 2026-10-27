-- Migración 014: Solicitud de Egreso Interno (Fase 6a)
-- Fecha: 2026-10-26
-- Descripción: Uso interno, merma y ajustes de inventario con reserva_stock.

CREATE TABLE IF NOT EXISTS solicitud_egreso_interno (
    id                        UUID PRIMARY KEY,
    tipo                      VARCHAR(20) NOT NULL
                              CHECK (tipo IN ('UsoInterno', 'Ajuste', 'Merma')),
    destino_uso_tipo          VARCHAR(20) NULL
                              CHECK (destino_uso_tipo IS NULL OR destino_uso_tipo IN ('Vivero', 'Laboratorio', 'Otro')),
    destino_uso_otro          VARCHAR(100) NULL,
    signo                     VARCHAR(10) NOT NULL
                              CHECK (signo IN ('Positivo', 'Negativo')),
    producto_id               UUID NULL REFERENCES producto(id),
    lote_id                   UUID NULL REFERENCES lote(id),
    cantidad                  NUMERIC(12,4) NOT NULL CHECK (cantidad > 0),
    unidad                    VARCHAR(30) NOT NULL,
    motivo                    TEXT NOT NULL,
    solicitante_id            UUID NOT NULL,
    estado                    VARCHAR(20) NOT NULL
                              CHECK (estado IN ('Pendiente', 'Aprobada', 'Rechazada', 'Confirmada')),
    resolucion_aprobador_id   UUID NULL,
    resolucion_fecha          TIMESTAMPTZ NULL,
    resolucion_motivo         TEXT NULL,
    entregado_por             UUID NULL,
    fecha_entrega             TIMESTAMPTZ NULL,
    creado_en                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_sol_producto_o_lote
        CHECK (producto_id IS NOT NULL OR lote_id IS NOT NULL),

    CONSTRAINT chk_sol_uso_interno_no_positivo
        CHECK (NOT (tipo = 'UsoInterno' AND signo = 'Positivo')),

    CONSTRAINT chk_sol_merma_no_positiva
        CHECK (NOT (tipo = 'Merma' AND signo = 'Positivo')),

    CONSTRAINT chk_sol_destino_uso
        CHECK (
            (tipo = 'UsoInterno' AND destino_uso_tipo IS NOT NULL) OR
            (tipo IN ('Ajuste', 'Merma') AND destino_uso_tipo IS NULL AND destino_uso_otro IS NULL)
        ),

    CONSTRAINT chk_sol_destino_otro
        CHECK (
            (destino_uso_tipo = 'Otro' AND destino_uso_otro IS NOT NULL AND TRIM(destino_uso_otro) <> '') OR
            (destino_uso_tipo IS NULL OR destino_uso_tipo <> 'Otro')
        ),

    CONSTRAINT chk_sol_estado_resolucion
        CHECK (
            (estado = 'Pendiente'
                AND resolucion_aprobador_id IS NULL
                AND resolucion_fecha IS NULL
                AND resolucion_motivo IS NULL
                AND entregado_por IS NULL
                AND fecha_entrega IS NULL) OR
            (estado = 'Aprobada'
                AND resolucion_aprobador_id IS NOT NULL
                AND resolucion_fecha IS NOT NULL
                AND resolucion_motivo IS NULL
                AND entregado_por IS NULL
                AND fecha_entrega IS NULL) OR
            (estado = 'Rechazada'
                AND resolucion_aprobador_id IS NOT NULL
                AND resolucion_fecha IS NOT NULL
                AND resolucion_motivo IS NOT NULL
                AND entregado_por IS NULL
                AND fecha_entrega IS NULL) OR
            (estado = 'Confirmada'
                AND resolucion_aprobador_id IS NULL
                AND resolucion_fecha IS NULL
                AND resolucion_motivo IS NULL
                AND entregado_por IS NOT NULL
                AND fecha_entrega IS NOT NULL) OR
            (estado = 'Confirmada'
                AND tipo = 'UsoInterno'
                AND resolucion_aprobador_id IS NULL
                AND entregado_por IS NOT NULL
                AND fecha_entrega IS NOT NULL)
        )
);

CREATE INDEX IF NOT EXISTS ix_solicitud_egreso_estado
    ON solicitud_egreso_interno(estado);
CREATE INDEX IF NOT EXISTS ix_solicitud_egreso_solicitante
    ON solicitud_egreso_interno(solicitante_id);
CREATE INDEX IF NOT EXISTS ix_solicitud_egreso_producto
    ON solicitud_egreso_interno(producto_id) WHERE producto_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_solicitud_egreso_lote
    ON solicitud_egreso_interno(lote_id) WHERE lote_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_solicitud_egreso_creado_en
    ON solicitud_egreso_interno(creado_en DESC);

-- Fase 6a: agregar la FK diferida de reserva_stock → solicitud_egreso_interno
ALTER TABLE reserva_stock
    DROP CONSTRAINT IF EXISTS fk_reserva_stock_solicitud;

ALTER TABLE reserva_stock
    ADD CONSTRAINT fk_reserva_stock_solicitud
    FOREIGN KEY (solicitud_id) REFERENCES solicitud_egreso_interno(id);

-- Fin migración 014.

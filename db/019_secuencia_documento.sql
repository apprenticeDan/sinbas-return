-- ============================================================
-- 019_secuencia_documento.sql — SINBAS F8: Secuencia atómica de documentos (OV, DSP)
-- Según impl_10.md §1 y plan_impl_10.md (Rama 05)
-- ============================================================

CREATE TABLE IF NOT EXISTS secuencia_documento (
    tipo    text NOT NULL,
    periodo int  NOT NULL,
    ultimo  int  NOT NULL DEFAULT 0,
    PRIMARY KEY (tipo, periodo)
);

-- Backfill OV (formato OV-YYYYMM-NNNN)
INSERT INTO secuencia_documento (tipo, periodo, ultimo)
SELECT
    'OV',
    CAST(SUBSTRING(codigo FROM 4 FOR 6) AS INT),
    MAX(CAST(SUBSTRING(codigo FROM 11) AS INT))
FROM orden_venta
WHERE codigo ~ '^OV-[0-9]{6}-[0-9]+$'
GROUP BY 1, 2
ON CONFLICT (tipo, periodo)
DO UPDATE SET ultimo = GREATEST(secuencia_documento.ultimo, EXCLUDED.ultimo);

-- Backfill DSP (formato DSP-YYYYMM-NNNN)
INSERT INTO secuencia_documento (tipo, periodo, ultimo)
SELECT
    'DSP',
    CAST(SUBSTRING(codigo FROM 5 FOR 6) AS INT),
    MAX(CAST(SUBSTRING(codigo FROM 12) AS INT))
FROM orden_despacho
WHERE codigo ~ '^DSP-[0-9]{6}-[0-9]+$'
GROUP BY 1, 2
ON CONFLICT (tipo, periodo)
DO UPDATE SET ultimo = GREATEST(secuencia_documento.ultimo, EXCLUDED.ultimo);

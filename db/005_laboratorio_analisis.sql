-- Migración 005: Tabla de Análisis de Laboratorio y Control de Calidad
-- Fecha: 2026-09-22
-- Descripción: Almacena resultados de ensayos de calidad (germinación, pureza, humedad, viabilidad) y dictámenes oficiales (F3 / RF06 / RN01 / RN12).

create table if not exists analisis_laboratorio (
    id                  uuid primary key,
    lote_id             uuid not null references lote(id) on delete cascade,
    fecha_analisis      date not null default current_date,
    germinacion         numeric(5,2) not null,
    pureza              numeric(5,2) not null,
    humedad             numeric(5,2) not null,
    viabilidad          numeric(5,2) not null,
    semillas_puras_kg   integer not null default 0,
    semillas_impurezas_kg integer not null default 0,
    dictamen            text not null, -- 'Aprobado' | 'Rechazado'
    observaciones       text
);

create index if not exists ix_analisis_lote_id on analisis_laboratorio(lote_id);
create index if not exists ix_analisis_fecha on analisis_laboratorio(fecha_analisis);

-- Semilla de análisis de laboratorio de prueba para lote SWIETMAC-02608-01 (Mara)
insert into analisis_laboratorio (
    id, lote_id, fecha_analisis, germinacion, pureza, humedad, viabilidad,
    semillas_puras_kg, semillas_impurezas_kg, dictamen, observaciones
)
values (
    '01917f3a-0005-7000-8000-000000000001',
    '01917f3a-0004-7000-8000-000000000001',
    '2026-08-20',
    88.50,
    95.00,
    8.00,
    90.00,
    18000,
    500,
    'Aprobado',
    'Lote de Mara con excelente vigor y pureza física'
)
on conflict (id) do nothing;

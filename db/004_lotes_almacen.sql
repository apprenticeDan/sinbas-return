-- Migración 004: Tabla de Lotes e Ingreso a Almacén
-- Fecha: 2026-08-27
-- Descripción: Permite la trazabilidad física por lote para semillas y plantines, registrando procedencia, cantidades y ubicación.

create table if not exists lote (
    id                  uuid primary key,
    codigo              text not null unique,
    producto_id         uuid not null references producto(id) on delete restrict,
    procedencia         text,
    cantidad_inicial    numeric(12,2) not null,
    cantidad_actual     numeric(12,2) not null,
    unidad              text not null,
    fecha_ingreso       date not null default current_date,
    ubicacion           text,
    estado              text not null default 'Activo',
    observaciones       text
);

create index if not exists ix_lote_producto_id on lote(producto_id);
create index if not exists ix_lote_estado on lote(estado);
create index if not exists ix_lote_codigo on lote(codigo);

-- Seed de lotes de prueba
insert into lote (
    id, codigo, producto_id, procedencia, cantidad_inicial, cantidad_actual,
    unidad, fecha_ingreso, ubicacion, estado, observaciones
)
values (
    '01917f3a-0004-7000-8000-000000000001',
    'SWIETMAC-02608-01',
    '01917f3a-0003-7000-8000-000000000001',
    'Bosque Chiquitano - Reserva Don Mario',
    50.00,
    50.00,
    'Kilogramo',
    '2026-08-15',
    'Almacén Central - Estante A1',
    'Activo',
    'Semilla recolectada en temporada alta con buena viabilidad'
)
on conflict (id) do nothing;

insert into lote (
    id, codigo, producto_id, procedencia, cantidad_inicial, cantidad_actual,
    unidad, fecha_ingreso, ubicacion, estado, observaciones
)
values (
    '01917f3a-0004-7000-8000-000000000002',
    'HANDIMPE-02608-01',
    '01917f3a-0003-7000-8000-000000000003',
    'Vivero Municipal Santa Cruz',
    2500.00,
    1800.00,
    'Gramo',
    '2026-08-20',
    'Cámara Fría B2',
    'Activo',
    'Semilla limpia procesada en laboratorio'
)
on conflict (id) do nothing;


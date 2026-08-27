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

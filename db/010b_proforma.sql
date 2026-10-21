-- ============================================================
-- 010b_proforma.sql — Feature F7: Proformas y Cotizaciones (011_proforma)
-- ============================================================

create table if not exists proforma (
    id                  uuid primary key,
    fecha               date not null default current_date,
    creado_en           timestamp with time zone not null default current_timestamp,
    responsable_id      uuid not null references usuario(id) on delete restrict,
    cliente_id          uuid references cliente(id) on delete set null,
    cliente_nombre_libre text,
    estado              text not null default 'Vigente',
    fecha_vencimiento   date,
    moneda              text not null default 'BOB',
    total               numeric(14,2) not null default 0,
    orden_venta_id      uuid,
    anulacion_motivo    text,
    anulado_por         uuid references usuario(id) on delete set null,
    anulado_en          timestamp with time zone,
    observaciones       text,
    leyenda             text not null default 'Precios y existencias referenciales sujetos a variación al momento de confirmación'
);

create index if not exists ix_proforma_estado on proforma(estado);
create index if not exists ix_proforma_cliente on proforma(cliente_id);
create index if not exists ix_proforma_fecha on proforma(fecha);
create index if not exists ix_proforma_responsable on proforma(responsable_id);

do $$ begin
    if not exists (select 1 from pg_constraint where conname = 'ck_proforma_estado') then
        alter table proforma add constraint ck_proforma_estado
            check (estado in ('Vigente', 'Vencida', 'Convertida', 'Anulada'));
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_proforma_total') then
        alter table proforma add constraint ck_proforma_total
            check (total >= 0);
    end if;
end $$;

create table if not exists linea_proforma (
    proforma_id         uuid not null references proforma(id) on delete cascade,
    item                integer not null,
    producto_id         uuid not null references producto(id) on delete restrict,
    cantidad            numeric(14,4) not null,
    unidad              text not null,
    precio_unitario     numeric(14,2) not null,
    subtotal            numeric(14,2) not null,
    primary key (proforma_id, item)
);

create index if not exists ix_linea_proforma_producto on linea_proforma(producto_id);

do $$ begin
    if not exists (select 1 from pg_constraint where conname = 'ck_linea_proforma_cantidad') then
        alter table linea_proforma add constraint ck_linea_proforma_cantidad
            check (cantidad > 0);
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_linea_proforma_precio') then
        alter table linea_proforma add constraint ck_linea_proforma_precio
            check (precio_unitario > 0);
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_linea_proforma_subtotal') then
        alter table linea_proforma add constraint ck_linea_proforma_subtotal
            check (subtotal >= 0);
    end if;
end $$;

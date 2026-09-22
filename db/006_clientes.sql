-- ============================================================
-- 006_clientes.sql — Gestión de Clientes (F6 / RF08 / CU-17 / RN18)
-- ============================================================

create table if not exists cliente (
    id                  uuid primary key,
    tipo                text not null, -- 'Natural' | 'Juridica'
    nombres             text,
    apellido_paterno    text,
    apellido_materno    text,
    ci_numero           text,
    ci_complemento      text,
    ci_extension        text,
    razon_social        text,
    nit                 text,
    rep_nombres         text,
    rep_apellido_paterno text,
    rep_apellido_materno text,
    rep_ci_numero       text,
    rep_ci_complemento  text,
    rep_ci_extension    text,
    rep_telefono        text,
    rep_email           text,
    telefono            text,
    email               text,
    direccion           text,
    estado              text not null default 'Activo'
);

create index if not exists ix_cliente_tipo on cliente(tipo);
create index if not exists ix_cliente_nit on cliente(nit);
create index if not exists ix_cliente_ci on cliente(ci_numero);
create index if not exists ix_cliente_razon_social on cliente(razon_social);

-- Clientes semilla
insert into cliente (
    id, tipo, nombres, apellido_paterno, apellido_materno, ci_numero, ci_complemento, ci_extension,
    telefono, email, direccion, estado
)
values (
    '01917f3a-0006-7000-8000-000000000001',
    'Natural',
    'Carlos',
    'Mendoza',
    'Vaca',
    '4892011',
    null,
    'CB',
    '+591 76543210',
    'carlos.mendoza@vivero.bo',
    'Av. Blanco Galindo Km 5, Cochabamba',
    'Activo'
)
on conflict (id) do nothing;

insert into cliente (
    id, tipo, razon_social, nit, rep_nombres, rep_apellido_paterno, rep_ci_numero, rep_ci_extension,
    telefono, email, direccion, estado
)
values (
    '01917f3a-0006-7000-8000-000000000002',
    'Juridica',
    'Agroforestal del Valle S.R.L.',
    '1028495029',
    'Ana',
    'Gómez',
    '5544332',
    'LP',
    '44234567',
    'contacto@agrovalle.com.bo',
    'Zona Industrial La Chimba #450, Cochabamba',
    'Activo'
)
on conflict (id) do nothing;

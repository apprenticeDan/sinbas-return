-- ============================================================
-- 001_auth.sql — Esquema del subsistema de autenticación
-- ============================================================

create table if not exists empleado (
    id              uuid primary key,
    nombres         text,
    apellido_paterno text,
    apellido_materno text,
    ci_numero       text,
    ci_complemento  text,
    telefono        text,
    email           text,
    nombre_completo text    not null,
    estado          text    not null default 'Activo'
);

alter table empleado add column if not exists nombres text;
alter table empleado add column if not exists apellido_paterno text;
alter table empleado add column if not exists apellido_materno text;
alter table empleado add column if not exists ci_numero text;
alter table empleado add column if not exists ci_complemento text;
alter table empleado add column if not exists telefono text;
alter table empleado add column if not exists email text;

create table if not exists usuario (
    id              uuid primary key,
    empleado_id     uuid    not null references empleado(id) on delete cascade,
    nombre_usuario  text    not null unique,
    password_hash   text    not null,
    estado          text    not null default 'Activo'
);

create index if not exists ix_usuario_nombre
    on usuario(nombre_usuario);

create table if not exists usuario_rol (
    usuario_id  uuid    not null references usuario(id) on delete cascade,
    rol         text    not null,
    primary key (usuario_id, rol)
);

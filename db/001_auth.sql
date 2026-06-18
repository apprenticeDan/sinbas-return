-- ============================================================
-- 001_auth.sql — Esquema del subsistema de autenticación
-- ============================================================

create table if not exists empleado (
    id              integer generated always as identity primary key,
    nombre_completo text    not null,
    estado          text    not null default 'Activo'
);

create table if not exists usuario (
    id              integer generated always as identity primary key,
    empleado_id     integer not null references empleado(id),
    nombre_usuario  text    not null unique,
    password_hash   text    not null,
    estado          text    not null default 'Activo'
);

create index if not exists ix_usuario_nombre
    on usuario(nombre_usuario);

create table if not exists usuario_rol (
    usuario_id  integer not null references usuario(id) on delete cascade,
    rol         text    not null,
    primary key (usuario_id, rol)
);

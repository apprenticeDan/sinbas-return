-- ============================================================
-- 007_refresh_tokens_idempotencia.sql — F0.1 (MF-00-05 y MF-00-07)
-- Manejo de expiración de sesión resiliente e idempotencia
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- Tabla: refresh_token
-- Almacena únicamente el hash SHA-256 de los refresh tokens.
-- El token crudo en texto plano solo viaja en cookies HttpOnly
-- al navegador y jamás se guarda en la base de datos.
--
-- Mecanismos de seguridad implementados:
-- 1. Rotación obligatoria: cada uso marca 'reemplazado_por'
--    con el nuevo ID generado.
-- 2. Detección de robo/reuso: si un token con 'reemplazado_por'
--    no nulo vuelve a presentarse, se revoca toda la familia
--    de tokens del usuario.
-- 3. Revocación en cascada: al desactivar un usuario (RN15),
--    todos sus tokens pasan a 'revocado = true'.
-- ─────────────────────────────────────────────────────────────
create table if not exists refresh_token (
    id              uuid primary key,
    usuario_id      uuid not null references usuario(id) on delete cascade,
    token_hash      text not null unique,
    expira_en       timestamp with time zone not null,
    revocado        boolean not null default false,
    reemplazado_por uuid references refresh_token(id) on delete set null,
    creado_en       timestamp with time zone not null default current_timestamp
);

create index if not exists ix_refresh_token_hash on refresh_token(token_hash);
create index if not exists ix_refresh_token_usuario on refresh_token(usuario_id);

-- ─────────────────────────────────────────────────────────────
-- Tabla: registro_idempotencia
-- Protege transacciones críticas (despacho, venta, cobro) contra
-- duplicación por reintentos de red o refresco de token.
-- ─────────────────────────────────────────────────────────────
create table if not exists registro_idempotencia (
    clave           text primary key,
    endpoint        text not null,
    usuario_id      uuid not null references usuario(id) on delete cascade,
    status_code     integer not null,
    cuerpo_respuesta text not null,
    creado_en       timestamp with time zone not null default current_timestamp
);

create index if not exists ix_idempotencia_creado on registro_idempotencia(creado_en);

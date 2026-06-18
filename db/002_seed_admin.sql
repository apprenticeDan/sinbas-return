-- ============================================================
-- 002_seed_admin.sql — Datos iniciales del administrador
--
-- Hash BCrypt de "admin123" generado con work factor 11.
-- Para regenerarlo: PasswordHasher.hash "admin123"
-- ============================================================

insert into empleado (nombre_completo, estado)
values ('Administrador del Sistema', 'Activo')
on conflict do nothing;

insert into usuario (empleado_id, nombre_usuario, password_hash, estado)
values (
    (select id from empleado where nombre_completo = 'Administrador del Sistema'),
    'admin',
    '$2a$11$8bv8pfyb92XqSnbzqIP9vuvXcnfCotYVp6Svj6ASNbYJTftltLBmu',
    'Activo'
)
on conflict (nombre_usuario) do nothing;

insert into usuario_rol (usuario_id, rol)
values (
    (select id from usuario where nombre_usuario = 'admin'),
    'Administrador'
)
on conflict do nothing;

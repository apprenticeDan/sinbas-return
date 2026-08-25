-- ============================================================
-- 002_seed_admin.sql — Datos iniciales del administrador
--
-- Hash BCrypt de "admin123" generado con work factor 11.
-- Para regenerarlo: PasswordHasher.hash "admin123"
-- ============================================================

insert into empleado (id, nombre_completo, estado)
values ('01917f3a-0001-7000-8000-000000000001', 'Administrador del Sistema', 'Activo')
on conflict (id) do nothing;

insert into usuario (id, empleado_id, nombre_usuario, password_hash, estado)
values (
    '01917f3a-0002-7000-8000-000000000002',
    '01917f3a-0001-7000-8000-000000000001',
    'admin',
    '$2a$11$8bv8pfyb92XqSnbzqIP9vuvXcnfCotYVp6Svj6ASNbYJTftltLBmu',
    'Activo'
)
on conflict (nombre_usuario) do nothing;

insert into usuario_rol (usuario_id, rol)
values (
    '01917f3a-0002-7000-8000-000000000002',
    'Administrador'
)
on conflict do nothing;

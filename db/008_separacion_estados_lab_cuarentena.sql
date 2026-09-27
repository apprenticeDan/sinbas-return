-- Migración 008: Separación de estados de Calidad y Ciclo de Vida de Lotes
-- Fecha: 2026-09-26
-- Descripción:
--   1. Capa 1 (Técnica): Dictámenes de calidad pasan a 'Aprobado' u 'Observado'. El laboratorio no rechaza unilateralmente.
--   2. Capa 2 (Comercial): Estado del lote incorpora 'EnCuarentena' para lotes observados (excluidos de stock vendible).
--   3. Capa 3 (Gobernanza): El estado 'Rechazado' es potestad exclusiva de Gerencia tras evaluar cuarentenas.

-- Documentación de estados válidos en tabla lote:
-- 'Activo', 'EnCuarentena', 'Rechazado', 'Agotado', 'Bloqueado', 'Archivado'

-- Documentación de dictámenes válidos en analisis_laboratorio:
-- 'Aprobado', 'Observado' (o 'Observado: <motivo>')

-- Si existieran lotes con estado 'Rechazado' previo que no fueron deliberados por Gerencia sino automáticos,
-- se documenta su trazabilidad. En nuevas operaciones, un dictamen observado asignará 'EnCuarentena'.

comment on column lote.estado is 'Estado comercial del lote: Activo, EnCuarentena, Rechazado, Agotado, Bloqueado, Archivado';
comment on column analisis_laboratorio.dictamen is 'Juicio técnico de laboratorio: Aprobado u Observado (con motivo técnico)';

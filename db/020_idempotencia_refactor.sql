BEGIN;
DELETE FROM registro_idempotencia;
ALTER TABLE registro_idempotencia
    ADD COLUMN request_hash text NOT NULL,
    ADD COLUMN expira_en    timestamptz NOT NULL;
ALTER TABLE registro_idempotencia DROP CONSTRAINT registro_idempotencia_pkey;
ALTER TABLE registro_idempotencia ADD CONSTRAINT registro_idempotencia_pkey
    PRIMARY KEY (usuario_id, endpoint, clave);
CREATE INDEX ix_idempotencia_expira ON registro_idempotencia(expira_en);
DROP INDEX IF EXISTS ix_idempotencia_creado;
COMMIT;


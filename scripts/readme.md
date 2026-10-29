## Base de datos

### Instalación limpia
```bash
bash scripts/db-apply-all.sh
```

### Reset operativo (mantener catálogo + usuarios + clientes)
```bash
podman exec -i sinbasreturn_sinbas-db_1 psql -U sinbas -d sinbas \
  -v ON_ERROR_STOP=1 -f - < scripts/db-reset-operational.sql
```
```bash
bash scripts/db-reset-operational.sql    # ops → 0, catálogo intacto
# Ahora crear los datos de prueba frescos
```


bash scripts/db-full-reset.sh
# → DB vacía + seed de admin + catálogo base (Swietenia, Cedrela, Handroanthus)
---


# Scripts de Base de Datos — SINBAS RETURN

Utilidades para gestionar el ciclo de vida de la base de datos en desarrollo.

## Índice

- [Convenciones de migraciones](#convenciones-de-migraciones)
- [Instalación limpia](#instalación-limpia)
- [Reset operativo](#reset-operativo)
- [Reset total](#reset-total)
- [Uso en flujos comunes](#uso-en-flujos-comunes)
- [Conexión manual a la DB](#conexión-manual-a-la-db)
- [Advertencias](#advertencias)

---

## Convenciones de migraciones

Las migraciones viven en `db/NNN_descripcion.sql`, numeradas **secuencialmente**
desde `001`. Cada archivo es **idempotente**: se puede re-ejecutar sin romper.

### Reglas

1. **Formato del nombre**: `NNN_descripcion_corta.sql`. Cero padding en el
   número (001, 002, ..., 010, 011), tres dígitos hasta 999.
2. **Idempotencia obligatoria**: usar `IF NOT EXISTS` / `IF EXISTS` /
   `ON CONFLICT DO NOTHING` en toda creación.
3. **Atomicidad**: cada migración debería ser atómica. Usar `BEGIN...COMMIT`
   solo si necesita validar pre-condiciones (ej. existencia de duplicados
   antes de crear un UNIQUE INDEX).
4. **Nunca borrar migraciones aplicadas**. Si una migración está mal, se
   corrige con una migración nueva (`018_fix_...`), no modificando la vieja.
5. **Seeds van en migraciones propias** (`002_seed_admin.sql`,
   `003_catalogo_productos.sql`). No mezclar DDL con DML masivo.

### Agregar una migración nueva

```bash
# 1. Crear el archivo
touch db/018_mi_cambio.sql

# 2. Escribir SQL idempotente
# 3. Aplicar solo esa
podman exec -i sinbasreturn_sinbas-db_1 psql -U sinbas -d sinbas \
  -v ON_ERROR_STOP=1 -f - < db/018_mi_cambio.sql

# 4. Commit
git add db/018_mi_cambio.sql
git commit -m "feat(db): <descripción>"

---

## En windows 
# 1. Detener backend primero (si está corriendo)
Get-Process | Where-Object {$_.ProcessName -like "Sinbas"} | Stop-Process -Force

# 2. Reset total
$DB = "sinbasreturn_sinbas-db_1"  # <-- ajustá si tu nombre difiere
podman exec -i $DB psql -U sinbas -d sinbas -v ON_ERROR_STOP=1 -c "TRUNCATE TABLE linea_movimiento, linea_despacho, linea_orden_venta, linea_proforma, analisis_laboratorio, reserva_stock, solicitud_egreso_interno, movimiento_inventario, orden_despacho, orden_venta, proforma, lote, cliente, producto, usuario_rol, refresh_token, registro_idempotencia, usuario, empleado CASCADE;"

# 3. Reaplicar migraciones
Get-ChildItem db\*.sql | Sort-Object Name | ForEach-Object {
    Write-Host "  → $($_.Name)"
    Get-Content $_.FullName | podman exec -i $DB psql -U sinbas -d sinbas -v ON_ERROR_STOP=1 -f -
}
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



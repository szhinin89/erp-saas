# InstallData

Datos de instalación cargados al iniciar la API. Política general: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**.

## Reglas

- Solo agregar archivos `.sql` numerados (`001_...sql`, `002_...sql`, etc.).
- Los scripts aplicados son **inmutables** (si cambian checksum, el arranque falla).
- Para cambios, crear un **nuevo archivo** con el siguiente número.
- Scripts destructivos (`DELETE`, `TRUNCATE`, `DROP` o `-- @destructive`) están bloqueados por defecto.

## Confirmación para scripts destructivos

Solo se habilitan cuando ambos valores están activos:

- `InstallData:AllowDestructive = true`
- `InstallData:ConfirmDestructive = CONFIRM_INSTALLDATA_DELETE`

Si no existe confirmación explícita, el proceso no ejecuta scripts destructivos.

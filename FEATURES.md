# Features — ERP SaaS

> Detalle de módulos y estado: [`docs/STATUS.md`](docs/STATUS.md)

## Módulos producto (tenant)

| Dominio | Rutas / API | Notas |
|---------|-------------|-------|
| Auth & acceso | `/login`, `/api/auth/*` | JWT + refresh rotation, RBAC |
| Catálogo | Productos, clientes, proveedores | ZH forms, validación 4 capas |
| Inventario | Bodegas, kardex, transferencias | RLS parcial |
| Ventas / SRI | Facturación electrónica | XML, firma P12, RIDE |
| Compras | OC, facturas proveedor | |
| Contabilidad | Plan de cuentas, asientos | Patrón referencia vertical |
| Configuración | Sucursales, perfiles, menú | Entitlements por plan |

## Plataforma SaaS

| Feature | Ruta | Notas |
|---------|------|-------|
| SuperAdmin | `/superadmin/*` | Panel global |
| Empresas / tenants | `/companies` | Sin UUID en URL (sessionStorage) |
| Planes SaaS | SuperAdmin planes | `SaasFeatureDefinition` |

## i18n

Español, English, **Kichwa de Cañar (`qu`)**.

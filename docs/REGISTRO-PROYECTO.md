# Registro del proyecto (ir agregando)

**Uso:** notas sueltas, pendientes, decisiones rápidas, enlaces a PRs, deuda técnica y recordatorios.  
**Convención sugerida:** una entrada por tema; fecha opcional al inicio de la línea o en subtítulo.

**Otros documentos relacionados:** `POLITICA-FORMULARIOS-Y-ACCESO.md`, `DESARROLLO.md`, `FRONTEND-PANTALLAS.md`, `STATUS-2026-05-ERP.md`.

---

## Pendientes / backlog

| Fecha | Tema | Nota | Enlace / PR |
|-------|------|------|-------------|
|       |      |      |             |

*(Agregar filas arriba o abajo; mantener la tabla o pasar a lista con viñetas si preferís.)*

---

## Decisiones y acuerdos (breve)

- 

---

## Ideas / investigación

- 

---

## Deuda técnica

- 

---

## Instalación / operación

### Orden recomendado (primera instalación)

Aplica a **desarrollo local**, **servidor del cliente** o **nube**: los pasos son los mismos; cambian **cadena de conexión**, **secretos** (variables del proveedor) y **URL** de la API.

| # | Paso | Qué hacer | Notas |
|---|------|-----------|--------|
| 1 | **Requisitos** | .NET SDK **10** (ver `backend/src/global.json`), **Node 22** (o 20+ para front), PostgreSQL accesible. | En local suele usarse Docker: `docker compose up -d` desde la raíz del monorepo — ver `README.md` / `DESARROLLO.md`. |
| 2 | **Código y configuración API** | Clonar/copiar el repo. Crear `backend/src/ERP.API/appsettings.Development.json` desde **`appsettings.Development.json.example`** (o configurar `appsettings.json` / variables de entorno en el host). | Ajustar **`ConnectionStrings:DefaultConnection`**, **`Jwt:SecretKey`** (y demás JWT). En nube: secret manager del servicio (App Service, Container Apps, etc.). |
| 3 | **Token instalación SuperAdmin** | Definir un valor fuerte para **`Deployment:InitialSuperAdminSetupToken`** (JSON) o **`Deployment__InitialSuperAdminSetupToken`** (env). | Solo para **crear el primer SuperAdmin**; no versionar el valor real en git. |
| 4 | **Base de datos** | Aplicar migraciones EF: desde `backend/src/ERP.Infrastructure` → `dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj`. | La DB debe existir y el usuario tener permisos DDL. |
| 5 | **Arrancar la API** | `cd backend/src` → `dotnet run --project ERP.API --launch-profile http` (o el perfil/publicación del entorno). | Comprobar Swagger en el puerto configurado (p. ej. **5003**). |
| 6 | **Crear SuperAdmin inicial** | Opción A: `pwsh ./scripts/create-superadmin.ps1 -SetupToken "…"` (o `-ApiBase https://tu-api` en nube). Opción B: `.\scripts\create-superadmin-interactive.ps1`. Opción C: `POST /api/setup/superadmin` con el cuerpo que define el backend. | Detalle en comentarios de `scripts/create-superadmin.ps1` y `SetupController`. Solo funciona si **aún no** existe SuperAdmin y el token coincide. |
| 7 | **Frontend** | `cd frontend` → `npm ci` (o `npm install`) → `npm run dev` (dev) o `npm run build` + hosting estático/preview (prod). | En dev, `VITE_API_URL` vacío suele usar **proxy** hacia la API (`vite.config.ts`). En prod, definir URL de API si no hay proxy. CORS: `Cors:AllowedOrigins` en la API. |
| 8 | **Post-instalación (operación)** | Crear tenants/usuarios según flujo de negocio; revisar **`Deployment:SuperAdminPanelEnabled`** para cerrar panel global en producción diaria si aplica — ver tabla en `DESARROLLO.md` § “Instalación en servidor del cliente”. | Opcional: `MaxActiveTenants`, `MaxIdentityUsers`. |

### Comandos rápidos (copiar y adaptar)

```powershell
# Raíz del monorepo erp-saas
docker compose up -d

cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj

cd ../..
$env:Deployment__InitialSuperAdminSetupToken = "TOKEN_SEGURO_SOLO_INSTALACION"
pwsh ./scripts/create-superadmin.ps1 -ApiBase "http://localhost:5003"

cd frontend
npm ci
npm run dev
```

*(En Linux/macOS: equivalente con `export Deployment__InitialSuperAdminSetupToken=...` y `bash` si no usás PowerShell.)*

### Documentación detallada

- **`docs/DESARROLLO.md`** — Docker, migraciones, arranque, CORS, troubleshooting, candado SuperAdmin.  
- **`README.md`** — visión general y enlace al orden de docs.

---

## Changelog informal (por fecha)

### YYYY-MM-DD

- 

---

## Enlaces útiles

| Recurso | URL / ruta |
|---------|------------|
| Swagger API | *(p. ej. `https://…/swagger` en el entorno que corresponda)* |
| CI | `.github/workflows/ci.yml` |

---

*Documento vivo: editar directamente en el repo.*

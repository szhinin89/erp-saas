# Runbook — Cómo correr y operar el sistema

## Prerequisitos

| Herramienta    | Versión mínima | Para qué                          |
|----------------|----------------|-----------------------------------|
| Docker Desktop | cualquiera     | PostgreSQL en contenedor          |
| .NET SDK       | 10.0           | Backend                           |
| Node.js        | 20             | Frontend                          |

## Levantar en desarrollo

### 1. Base de datos

```powershell
# Verificar que el contenedor esté corriendo
docker ps --filter "name=postgreszh"

# Si no existe, crearlo:
docker run -d \
  --name postgreszh \
  -e POSTGRES_PASSWORD=zhin@2024 \
  -p 5435:5432 \
  postgres:16
```

Cadena de conexión (ya configurada en `appsettings.json`):
```
Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024
```

### 2. Aplicar migraciones

```powershell
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
```

### 3. Backend

```powershell
cd backend/src
dotnet run --project ERP.API --launch-profile http
```

Disponible en:
- API: `http://localhost:5003`
- Swagger: `http://localhost:5003/swagger`

### 4. Frontend

```powershell
cd frontend
npm install        # solo la primera vez
npm run dev
```

Disponible en: `http://localhost:5173`

---

## Primer uso (datos de prueba)

### Crear un tenant
```bash
curl -X POST http://localhost:5003/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Mi Empresa","slug":"mi-empresa"}'
```
Guardar el `id` retornado.

### Registrar usuario
```bash
curl -X POST http://localhost:5003/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Admin",
    "lastName": "ERP",
    "email": "admin@miempresa.com",
    "password": "Admin1234!",
    "tenantId": "<id-del-tenant>",
    "role": "Admin"
  }'
```

### Login en el frontend
Abrir `http://localhost:5173`, ingresar el `tenantId`, email y contraseña.

---

## Comandos frecuentes

### Backend

```powershell
# Build de toda la solución
cd backend/src && dotnet build ERP.slnx

# Correr tests
cd backend/src && dotnet test ERP.slnx

# Nueva migración
cd backend/src/ERP.Infrastructure
dotnet ef migrations add NombreMigracion --startup-project ../ERP.API

# Revertir última migración
dotnet ef migrations remove --startup-project ../ERP.API
```

### Frontend

```powershell
cd frontend

npm run dev        # desarrollo con HMR
npm run build      # build de producción (output en dist/)
npm run lint       # ESLint
npx tsc --noEmit   # type-check sin emitir archivos
```

---

## Endpoints disponibles

| Método | Ruta                                    | Auth | Descripción                    |
|--------|-----------------------------------------|------|--------------------------------|
| POST   | /api/auth/register                      | No   | Registrar usuario              |
| POST   | /api/auth/login                         | No   | Login → retorna JWT            |
| POST   | /api/tenants                            | No   | Crear tenant                   |
| GET    | /api/products                           | JWT  | Listar productos del tenant    |
| GET    | /api/products/{id}                      | JWT  | Obtener producto               |
| POST   | /api/products                           | JWT  | Crear producto                 |
| GET    | /api/accounts                           | JWT  | Listar cuentas contables       |
| GET    | /api/accounts/{id}                      | JWT  | Obtener cuenta                 |
| POST   | /api/accounts                           | JWT  | Crear cuenta                   |
| GET    | /api/accounts/journal-entries           | JWT  | Listar asientos contables      |
| GET    | /api/accounts/journal-entries/{id}      | JWT  | Obtener asiento con líneas     |
| POST   | /api/accounts/journal-entries           | JWT  | Crear asiento contable         |

---

## Solución de problemas frecuentes

### El backend no inicia — error de conexión a la DB
Verificar que Docker esté corriendo y el contenedor `postgreszh` esté activo.

### Error 401 en el frontend
El token JWT venció (duración: 60 min). Hacer logout y login nuevamente.

### Error al compilar — archivos bloqueados
El proceso `ERP.API` está corriendo. Detenerlo antes de compilar:
```powershell
Stop-Process -Name "ERP.API" -Force
```

### Error de CORS en el navegador
Verificar que `Cors:AllowedOrigins` en `appsettings.Development.json` incluya `http://localhost:5173`.

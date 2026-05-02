# Continuar después de esta sesión

## Qué quedó hecho (hilo funcional)

- **SuperAdmin único por servidor:** flujo `POST /api/setup/superadmin` (token `Deployment:InitialSuperAdminSetupToken`), login `superadmin-login`, panel `/superadmin`.
- **Cuotas de instancia:** `DedicatedSingleClientInstance`, `MaxActiveTenants`, `MaxIdentityUsers`, `MaxUsersPerTenant`; archivo opcional `App_Data/instance-quota.json` (ignorado en git); API `GET/PUT /api/superadmin/instance-quota`.
- **Infra:** `InstanceQuotaFileStore` con `IHostEnvironment` + paquete `Microsoft.Extensions.Hosting.Abstractions`; `Program.cs` llama `AddUserSecrets` para que el token de setup gane sobre `appsettings` vacío.
- **Proyecto API:** `UserSecretsId` en `ERP.API.csproj` para `dotnet user-secrets`.
- **Frontend:** `/superadmin/instance-quota`, contexto de despliegue ampliado, i18n es/en/qu.
- **Scripts:** `scripts/create-superadmin.ps1` y `scripts/create-superadmin-interactive.ps1` (asistente por pasos).

## Cómo retomar en local

1. PostgreSQL y migraciones al día.
2. `cd backend/src/ERP.API` → `dotnet user-secrets list` (token) → `dotnet run` (puerto típico **5003**).
3. Si aún no hay SuperAdmin: desde repo `erp-saas` → `.\scripts\create-superadmin-interactive.ps1` (o el `.ps1` no interactivo con `-SetupToken`).
4. Frontend: `cd frontend` → `npm run dev` (ajustar `VITE_API_URL` si aplica).

## Recordatorios

- Tras cambiar **user-secrets** o **Program.cs**, **reiniciar** la API. Si `dotnet build` falla por exe en uso, cerrar el proceso **ERP.API** antes.
- **Un solo SuperAdmin** por base de datos; modo dedicado exige **máximo de empresas (RUC) &gt; 0** (no ilimitado en archivo).

## Archivos de referencia rápida

| Área | Ruta |
|------|------|
| Setup SuperAdmin | `backend/src/ERP.API/Controllers/SetupController.cs` |
| Cuotas API | `backend/src/ERP.API/Controllers/SuperAdminController.cs` (`instance-quota`) |
| Flags despliegue | `backend/src/ERP.Infrastructure/Deployment/DeploymentFeatureFlags.cs` |
| Pantalla cuotas SPA | `frontend/src/pages/SuperAdminInstanceQuotaPage.tsx` |
| Scripts | `scripts/create-superadmin.ps1`, `scripts/create-superadmin-interactive.ps1` |

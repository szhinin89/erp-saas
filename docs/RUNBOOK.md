## Runbook (cómo correr y probar)

### Backend (Swagger)

Desde:

```powershell
cd c:\ProyectCursor\erp-saas\backend\src
dotnet run --project .\ERP.API\ERP.API.csproj --environment Development
```

Swagger:

- `https://localhost:7253/swagger`
- `http://localhost:5003/swagger`

### Backend (tests)

```powershell
cd c:\ProyectCursor\erp-saas\backend\src
dotnet test .\ERP.slnx -c Release
```

### Frontend

```powershell
cd c:\ProyectCursor\erp-saas\frontend
npm run dev
```

Build/lint:

```powershell
cd c:\ProyectCursor\erp-saas\frontend
npm run lint
npm run build
```


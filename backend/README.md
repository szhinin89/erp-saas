# Backend — ERP SaaS (.NET 10)

Monolito modular Clean Architecture.

## Layout

```
backend/
├── src/
│   ├── ERP.Domain/
│   ├── ERP.Application/
│   ├── ERP.Infrastructure/
│   ├── ERP.API/
│   ├── ERP.*.Tests/
│   ├── ERP.Architecture.Tests/
│   └── ERP.slnx
├── scripts/       # SQL ops legacy (migrar a infrastructure/)
├── tools/         # Reservado
└── docs/          # Notas específicas backend
```

## Comandos

```powershell
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API --launch-profile http   # :5003
dotnet test ERP.slnx -c Release
```

## Reglas

[`BACKEND_RULES.md`](../BACKEND_RULES.md) · [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md)

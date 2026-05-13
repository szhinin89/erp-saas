# Infraestructura — estructura y convenciones (esta carpeta)

> **Normativa global del producto:** [`docs/ARCHITECTURE.md`](../../../docs/ARCHITECTURE.md) (raíz del monorepo `erp-saas`).  
> Este archivo describe **solo** la organización interna de `ERP.Infrastructure/` y convenciones de servicios/repositorios; no repite el modelo de capas completo.

## Estructura

```
Infrastructure/
├── Authentication/
│   ├── Services/
│   │   ├── AccessTokenService.cs      → arranque y tokens de sesión
│   │   ├── CurrentUserService.cs      → Extrae claims del usuario
│   │   └── JwtService.cs              → Validación y manejo de JWT
│   └── Security/
│       └── BcryptPasswordHasher.cs    → Implementación de hashing (puerto IPasswordHasher)
├── SaaS/
│   ├── Services/
│   │   ├── SubscriptionService.cs     → Lógica de suscripciones y planes
│   │   └── ConfigService.cs           → Configuración de features SaaS
│   └── Repositories/
│       └── SaaS-specific repos aquí
├── Configuration/
│   ├── Services/
│   │   └── Servicios de config global
│   └── Repositories/
├── Persistence/
│   ├── Data/
│   │   └── ErpDbContext.cs            → DbContext centralizado
│   └── Repositories/
│       └── Implementaciones de repositorios
├── Deployment/                         → lógica de despliegue
├── Seeding/                            → datos iniciales (seed)
└── DependencyInjection.cs              → Registro de servicios
```

## 🎯 Convenciones

1. **Nombres de carpetas**: Singular (Authentication, no Authentications)
2. **Servicios**: `XyzService.cs` + interfaz `IXyzService.cs` en Application
3. **Repositorios**: `IXyzRepository.cs` en Domain, implementaciones aquí
4. **Seguridad**: Contraseñas, JWT, criptografía en `Authentication/Security/`
5. **Tenant-awareness**: Todos los servicios deben considerar multi-tenancy

## Multi-tenant (patrón)

- `ICurrentTenant` inyectado en servicios que necesitan contexto de tenant
- Filtros automáticos en `ErpDbContext.OnModelCreating()`
- Sin excepciones por defecto: todo el acceso a datos por tenant debe respetar el aislamiento acordado

## Próximos pasos

- Separar `ErpDbContext` por módulo (Products, Accounting, SaaS con convenciones)
- Crear repositorios específicos por bounded context
- Mover lógica de lectura/admin a CQRS handlers

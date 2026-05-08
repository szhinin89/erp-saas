# Infrastructure - Arquitectura Modular por Bounded Context

## 📋 Estructura

```
Infrastructure/
├── Authentication/
│   ├── Services/
│   │   ├── AccessTokenService.cs      → Bootstrap + Session tokens
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
├── Deployment/                         → Lógica de deployment
├── Seeding/                            → Data seeding
└── DependencyInjection.cs              → Registro de servicios
```

## 🎯 Convenciones

1. **Nombres de carpetas**: Singular (Authentication, no Authentications)
2. **Servicios**: `XyzService.cs` + interfaz `IXyzService.cs` en Application
3. **Repositorios**: `IXyzRepository.cs` en Domain, implementaciones aquí
4. **Seguridad**: Contraseñas, JWT, criptografía en `Authentication/Security/`
5. **Tenant-awareness**: Todos los servicios deben considerar multi-tenancy

## 🔄 Multi-Tenancy Pattern

- `ICurrentTenant` inyectado en servicios que necesitan contexto de tenant
- Filtros automáticos en `ErpDbContext.OnModelCreating()`
- No hay excepciones: TODO se filtra por tenant

## 📦 Próximos Pasos

- Separar `ErpDbContext` por módulo (Products, Accounting, SaaS con convenciones)
- Crear repositorios específicos por bounded context
- Mover lógica de lectura/admin a CQRS handlers

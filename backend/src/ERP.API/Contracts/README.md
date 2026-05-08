# API Contracts - Separación de Responsabilidades

## 📋 Convención: Request DTO → Domain Filter

### Problema
Los controllers estaban construyendo filtros manualmente en el método:

```csharp
// ❌ ANTES: Responsabilidad mixta en el controller
[HttpGet("report")]
public async Task<IActionResult> GetReport(
    [FromQuery] string? search,
    [FromQuery] string? saleCode,
    // ... 12 parámetros más
    CancellationToken ct = default)
{
    var filter = new ProductReportFilter(
        Search: search,
        SaleCode: saleCode,
        // ... constructor de 14 parámetros
    );
    // ...
}
```

### Solución
Crear un **Request DTO explícito** en ERP.API/Contracts:

```csharp
// ✅ DESPUÉS: Responsabilidades separadas

// 1. API Contract (qué recibe el endpoint HTTP)
[HttpGet("report")]
public async Task<IActionResult> GetReport(
    [FromQuery] GetProductReportRequest request,
    CancellationToken ct = default)
{
    // 2. Mapeo explícito request → filter
    var filter = request.ToFilter();
    
    // 3. Pasar al handler de negocio
    var result = await _mediator.Send(
        new GetProductReportQuery(filter, request.PageNumber, request.PageSize), ct);
    // ...
}
```

### Ventajas

1. **Contrato de API claro**: Swagger documenta automáticamente `GetProductReportRequest`
2. **Validaciones centralizadas**: Agregar `[Required]`, `[Range]`, etc. en el DTO
3. **Separación**: API request ≠ Domain filter
4. **Versionabilidad**: Si el API cambia, el filter de negocio no se ve afectado
5. **Reutilización**: `ToFilter()` puede ser usado por múltiples handlers

### Estructura

```
ERP.API/
└── Contracts/
    ├── Products/
    │   ├── GetProductReportRequest.cs      ← Nuevo DTO
    │   └── ProductDto.cs                   ← DTO de respuesta
    ├── Companies/
    │   ├── CreateCompanyRequest.cs
    │   └── CompanyDto.cs
    └── ...
```

### Próximos Pasos

1. Crear `GetProductReportRequest` con método `ToFilter()`
2. Actualizar `ProductsController.GetReport()` para recibirlo
3. Aplicar el patrón a otros endpoints con múltiples parámetros
4. Considerar FluentValidation si necesita reglas complejas


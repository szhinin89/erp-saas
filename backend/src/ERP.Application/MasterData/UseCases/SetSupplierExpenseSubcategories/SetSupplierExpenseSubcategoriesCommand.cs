using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — fija el conjunto deseado de subcategorías de gasto de un
/// proveedor en la empresa activa (reemplazo atómico): crea las nuevas, reactiva las retiradas
/// reutilizando su registro y retira (baja lógica) las ausentes. Un conjunto vacío retira todas las
/// asignaciones activas (el proveedor queda «sin configuración»); nunca hay DELETE físico.
/// Company-scoped: tenant y empresa solo del contexto, nunca del body.
/// </summary>
public sealed record SetSupplierExpenseSubcategoriesCommand(
    Guid BusinessPartnerId,
    IReadOnlyList<Guid> ExpenseSubcategoryIds
) : IRequest<Result<SupplierExpenseSubcategoriesDto>>, ICompanyScopedRequest;

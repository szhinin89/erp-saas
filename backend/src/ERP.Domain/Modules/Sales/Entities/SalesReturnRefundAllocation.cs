using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// Asignación explícita de reembolso elegida por el operador al autorizar una <see cref="SalesReturn"/>
/// (P0-01, decisión de producto 2 — sin prorrateo automático entre formas de pago). Representa
/// exclusivamente la elección del operador; no ejecuta ningún efecto financiero — eso lo hace la
/// capa de Application en una fase posterior (Caja/CxC).
/// </summary>
public sealed class SalesReturnRefundAllocation : IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReturnId { get; private set; }
    public SalesReturnRefundMethod Method { get; private set; }
    public decimal Amount { get; private set; }

    private SalesReturnRefundAllocation() { }

    public static SalesReturnRefundAllocation Create(
        Guid returnId,
        Guid tenantId,
        SalesReturnRefundMethod method,
        decimal amount
    )
    {
        if (returnId == Guid.Empty)
            throw new ArgumentException("La devolución es obligatoria.", nameof(returnId));
        if (!Enum.IsDefined(method))
            throw new ArgumentException(
                "La forma de reembolso es obligatoria.",
                nameof(method)
            );
        if (amount <= 0)
            throw new ArgumentException(
                "El monto de la asignación de reembolso debe ser mayor a cero.",
                nameof(amount)
            );

        return new SalesReturnRefundAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReturnId = returnId,
            Method = method,
            Amount = amount,
        };
    }
}

using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>Fila del listado de anticipos de clientes.</summary>
public sealed record CustomerAdvanceListItemDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerName,
    Guid BranchId,
    DateOnly AdvanceDate,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AvailableAmount,
    string Status,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string? ExternalReference
);

/// <summary>Detalle de un anticipo con su historial completo de movimientos.</summary>
public sealed record CustomerAdvanceDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerName,
    Guid BranchId,
    DateOnly AdvanceDate,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AvailableAmount,
    string Status,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    string? ExternalReference,
    string? Note,
    IReadOnlyList<CustomerAdvanceMovementDto> Movements
);

/// <summary>Movimiento del libro único del anticipo, en orden cronológico.</summary>
public sealed record CustomerAdvanceMovementDto(
    Guid Id,
    string MovementType,
    decimal Amount,
    int LineNumber,
    /// <summary>Operación a la que pertenece el movimiento; identifica una aplicación a revertir.</summary>
    Guid OperationId,
    Guid BranchId,
    DateOnly EffectiveDate,
    DateOnly? EvidenceDate,
    string? Reason,
    string? Reference,
    Guid? ReceivableId,
    Guid? InstallmentId,
    Guid? ReversalOfMovementId,
    Guid? CashSessionId,
    Guid? CashMovementId,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc
);

/// <summary>Aplicación de anticipo recibida por una CxC, vigente o ya revertida.</summary>
public sealed record CustomerAdvanceReceivableApplicationDto(
    Guid CustomerAdvanceId,
    Guid OperationId,
    Guid MovementId,
    Guid? InstallmentId,
    decimal Amount,
    DateOnly EffectiveDate,
    Guid BranchId,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    bool IsReversed,
    Guid? ReversalMovementId,
    DateOnly? ReversalDate
);

/// <summary>Saldo disponible de un cliente en la empresa activa, sin distinguir sucursal.</summary>
public sealed record CustomerAdvanceBalanceDto(
    Guid CustomerId,
    string CurrencyCode,
    decimal AvailableAmount
);

/// <summary>Página del listado de anticipos.</summary>
public sealed record CustomerAdvanceListResultDto(
    IReadOnlyList<CustomerAdvanceListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

internal static class CustomerAdvanceMap
{
    public static CustomerAdvanceListItemDto ToListItem(CustomerAdvance a, string? customerName) =>
        new(
            a.Id,
            a.CustomerId,
            customerName,
            a.BranchId,
            a.AdvanceDate,
            a.CurrencyCode,
            a.OriginalAmount,
            a.AvailableAmount,
            a.Status.ToString(),
            a.PaymentMethodId,
            a.CompanyBankAccountId,
            a.CashRegisterId,
            a.ExternalReference
        );

    public static CustomerAdvanceDto ToDto(CustomerAdvance a, string? customerName) =>
        new(
            a.Id,
            a.CustomerId,
            customerName,
            a.BranchId,
            a.AdvanceDate,
            a.CurrencyCode,
            a.OriginalAmount,
            a.AvailableAmount,
            a.Status.ToString(),
            a.PaymentMethodId,
            a.CompanyBankAccountId,
            a.CashRegisterId,
            a.ExternalReference,
            a.Note,
            a.Movements.OrderBy(m => m.CreatedAtUtc)
                .ThenBy(m => m.LineNumber)
                .Select(ToDto)
                .ToList()
        );

    public static CustomerAdvanceReceivableApplicationDto ToApplication(
        CustomerAdvanceMovement application,
        CustomerAdvanceMovement? reversal
    ) =>
        new(
            application.CustomerAdvanceId,
            application.ClientRequestId,
            application.Id,
            application.InstallmentId,
            application.Amount,
            application.EffectiveDate,
            application.BranchId,
            application.CreatedByUserId,
            application.CreatedAtUtc,
            reversal is not null,
            reversal?.Id,
            reversal?.EffectiveDate
        );

    private static CustomerAdvanceMovementDto ToDto(CustomerAdvanceMovement m) =>
        new(
            m.Id,
            m.MovementType.ToString(),
            m.Amount,
            m.LineNumber,
            m.ClientRequestId,
            m.BranchId,
            m.EffectiveDate,
            m.EvidenceDate,
            m.Reason,
            m.Reference,
            m.ReceivableId,
            m.InstallmentId,
            m.ReversalOfMovementId,
            m.CashSessionId,
            m.CashMovementId,
            m.CreatedByUserId,
            m.CreatedAtUtc
        );
}

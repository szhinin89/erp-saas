namespace ERP.Application.Modules.Finance.DTOs;

public sealed record CreditTermDto(
    Guid Id,
    string Code,
    string Name,
    string Mode,
    int TotalDays,
    bool IsActive,
    IReadOnlyList<CreditInstallmentDto> Installments,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record CreditInstallmentDto(
    Guid Id,
    int InstallmentNumber,
    int DaysOffset,
    decimal Percentage
);

// ── Payments (Fase 5.5.5.3 — liquidación AR/AP) ──────────────────────────

public sealed record PaymentApplicationLineDto(
    Guid Id,
    Guid? ReceivableId,
    Guid? PayableId,
    Guid? InstallmentId,
    decimal AppliedAmount
);

public sealed record PaymentDto(
    Guid Id,
    string Direction,
    Guid PartnerId,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? PaymentMethodId,
    string? Reference,
    string Status,
    DateTime? AppliedAtUtc,
    DateTime? ReversedAtUtc,
    string? ReverseReason,
    IReadOnlyList<PaymentApplicationLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>Entrada de una línea de aplicación al registrar un cobro/pago — DocumentId es SalesReceivable.Id o PurchasePayable.Id según el comando.</summary>
public sealed record PaymentApplicationLineInput(
    Guid DocumentId,
    Guid? InstallmentId,
    decimal AppliedAmount
);

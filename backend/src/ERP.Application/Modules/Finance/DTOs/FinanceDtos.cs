namespace ERP.Application.Modules.Finance.DTOs;

public sealed record CreditTermDto(
    Guid Id, string Code, string Name, string Mode, int TotalDays,
    bool IsActive, IReadOnlyList<CreditInstallmentDto> Installments,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CreditInstallmentDto(
    Guid Id, int InstallmentNumber, int DaysOffset, decimal Percentage);

namespace ERP.Application.Modules.Caja.DTOs;

/// <param name="EmissionType">
/// Resuelto en vivo desde <c>EmissionPoint.EmissionType</c> (nunca un snapshot) usando el
/// <c>EmissionPointId</c> ya fijado en la sesión — null si el punto de emisión ya no existe/está
/// activo, nunca un valor inventado por defecto.
/// </param>
public sealed record CashSessionDto(
    Guid Id, Guid CompanyId, Guid BranchId, Guid UserId,
    Guid CashRegisterId, string CashRegisterCodeSnapshot, string CashRegisterNameSnapshot,
    Guid EmissionPointId, string EmissionPointCodeSnapshot, string? EmissionType,
    Guid? DefaultWarehouseId, string? DefaultWarehouseName,
    Guid? DefaultCustomerId, string? DefaultCustomerName,
    DateTime OpenedAt, decimal OpeningAmount,
    string Status, string? Notes,
    DateTime? ClosedAt, Guid? ClosedBy, string? CloseNotes,
    decimal? ExpectedAmount, decimal? CountedAmount, decimal? Difference,
    decimal TotalIncome, decimal TotalExpense, decimal CurrentBalance,
    IReadOnlyList<CashMovementDto> Movements,
    IReadOnlyList<CashClosingCountDto> ClosingCounts,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CashMovementDto(
    Guid Id, string MovementType, decimal Amount,
    string Description, DateTime CreatedAt, Guid CreatedBy,
    string ReferenceType, Guid? ReferenceId, string? ReferenceNumber);

public sealed record CashClosingCountDto(
    Guid Id, decimal DenominationValue, string DenominationLabel,
    int Quantity, decimal Total);

public sealed record CashSessionListDto(
    Guid Id, Guid UserId, Guid CashRegisterId, string CashRegisterCodeSnapshot, Guid EmissionPointId,
    DateTime OpenedAt, decimal OpeningAmount, string Status,
    decimal CurrentBalance, int MovementCount,
    DateTime? ClosedAt, decimal? Difference,
    DateTime CreatedAt);

public sealed record CashSessionListResponse(
    IReadOnlyList<CashSessionListDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// DTO único de Caja — alimenta a la vez el selector "Abrir Caja" (con tarjeta resumen
/// Sucursal/Establecimiento/Punto de Emisión) y la administración de Cajas, evitando
/// requests adicionales y endpoints duplicados.
/// </summary>
/// <param name="HasHistory">
/// true si la Caja ya tiene historial operativo (ver <c>ICashRegisterUsageGuard</c>) — único
/// indicador que el frontend debe usar para bloquear Código/Sucursal/Punto de Emisión en el
/// formulario de edición. Calculado siempre server-side, nunca inferido en el cliente.
/// </param>
public sealed record CashRegisterDto(
    Guid Id, Guid BranchId, string BranchName, string? BranchCode,
    Guid? EmissionPointId, string? EstablishmentCode,
    string? EmissionPointCode, string? EmissionPointName,
    string Code, string Name, string? Notes, bool IsActive,
    bool HasHistory,
    Guid? DefaultWarehouseId, string? DefaultWarehouseCode, string? DefaultWarehouseName,
    Guid? DefaultCustomerId, string? DefaultCustomerName,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record EmissionPointLookupForBranchDto(
    Guid Id, string Code, string? Name, string EstablishmentCode);

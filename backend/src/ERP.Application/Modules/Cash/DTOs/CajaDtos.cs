namespace ERP.Application.Modules.Cash.DTOs;

public sealed record BankAccountDto(
    Guid Id,
    string  Name,
    string NumeroCuenta,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    decimal SaldoActual,
    bool Activo,
    Guid? CuentaContableId);

public sealed record BankStatementDto(
    Guid Id,
    Guid BankAccountId,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal OpeningBalance,
    decimal ClosingBalance,
    DateTime FechaCarga,
    bool Conciliado,
    int CantidadRows);

public sealed record BankTransactionDto(
    Guid Id,
    Guid BankStatementId,
    DateTime Fecha,
    string  Description,
    decimal Monto,
    string Tipo,
    string Referencia,
    Guid?     JournalEntryId,
    string    Status);

public sealed record SugerenciaConciliacionDto(
    Guid BankTransactionId,
    Guid? AsientoContableSugeridoId,
    string? Reason);

public sealed record FlujoEfectivoDiaDto(DateOnly Fecha, decimal Entradas, decimal Salidas, decimal Neto);

public sealed record GastoPettyCashDto(
    Guid Id,
    Guid PettyCashId,
    DateTime Fecha,
    string Concepto,
    decimal Monto,
    string TipoComprobante,
    string NumeroComprobante,
    Guid?     JournalEntryId);

public sealed record CashCountDto(
    Guid Id,
    Guid PettyCashId,
    DateTime FechaArqueo,
    decimal EfectivoFisico,
    decimal Diferencia,
    string Observaciones,
    bool Aprobado);

public sealed record PettyCashDto(
    Guid Id,
    string  Name,
    decimal SaldoAsignado,
    decimal SaldoActual,
    Guid? BankAccountIdReposicion,
    Guid? CuentaContableCajaId,
    bool Activo);

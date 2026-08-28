using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;

namespace ERP.Application.Modules.Payables.UseCases;

/// <summary>PAYABLES-PURCHASE-MIGRATION-10 — una cuota del cronograma a crear junto con la CxP.</summary>
public sealed record AccountsPayableInstallmentInput(int InstallmentNumber, DateOnly DueDate, decimal Amount);

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 / PAYABLES-PURCHASE-MIGRATION-10 — payload para crear una CxP
/// genérica a partir de un documento de origen (Compra, Gasto o manual). No es un <c>IRequest</c> de
/// MediatR: es un caso de uso interno, invocado directamente por el módulo de origen (Gastos vía
/// <c>ConfirmExpenseDocumentHandler</c>, Compras vía <c>ConfirmPurchaseHandler</c>) después de que
/// su propio flujo (posting incluido) ya tuvo éxito — nunca expuesto vía API en esta fase.
/// <see cref="Installments"/> reemplaza el par <c>DueDate</c>+<c>TotalAmount</c> original
/// (Foundation-09): la mayoría de orígenes siguen generando una sola cuota, pero Compras con
/// condición de pago a plazos ahora migra su cronograma completo a
/// <see cref="AccountsPayableInstallment"/>.
/// </summary>
public sealed record CreateAccountsPayableFromOriginRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    Guid SupplierId,
    AccountsPayableOriginType OriginType,
    Guid OriginId,
    string DocumentType,
    string DocumentNumber,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    IReadOnlyList<AccountsPayableInstallmentInput> Installments
);

public interface IAccountsPayableService
{
    /// <summary>
    /// Idempotente por (TenantId, CompanyId, OriginType, OriginId): si ya existe una CxP para ese
    /// origen, la devuelve tal cual (nunca crea un duplicado, nunca falla por "ya existe" — un
    /// reintento del módulo de origen, p. ej. tras un timeout de red, debe ser seguro). Crea una
    /// cuota por cada entrada de <paramref name="request"/>.Installments, en el orden dado.
    /// Persiste inmediatamente (<c>SaveChangesAsync</c> propio) — usar
    /// <see cref="StageFromOriginAsync"/> en su lugar cuando el llamador necesita que la creación
    /// forme parte de una transacción/SaveChanges más grande ya en curso (p. ej. Compras, donde la
    /// CxP debe confirmarse atómicamente junto con el resto de la confirmación de la factura).
    /// </summary>
    Task<AccountsPayable> CreateFromOriginAsync(
        CreateAccountsPayableFromOriginRequest request,
        Guid createdBy,
        CancellationToken ct = default
    );

    /// <summary>
    /// Misma idempotencia y creación que <see cref="CreateFromOriginAsync"/>, pero solo deja la CxP
    /// en staging (<c>IAccountsPayableRepository.AddAsync</c>) — el llamador es responsable de
    /// invocar su propio <c>SaveChangesAsync</c> (o equivalente) para persistirla, como parte de una
    /// transacción más grande.
    /// </summary>
    Task<AccountsPayable> StageFromOriginAsync(
        CreateAccountsPayableFromOriginRequest request,
        Guid createdBy,
        CancellationToken ct = default
    );
}

public sealed class AccountsPayableService : IAccountsPayableService
{
    private readonly IAccountsPayableRepository _repo;

    public AccountsPayableService(IAccountsPayableRepository repo) => _repo = repo;

    public async Task<AccountsPayable> CreateFromOriginAsync(
        CreateAccountsPayableFromOriginRequest request,
        Guid createdBy,
        CancellationToken ct = default
    )
    {
        var existing = await _repo.GetByOriginAsync(
            request.TenantId,
            request.CompanyId,
            request.OriginType,
            request.OriginId,
            ct
        );
        if (existing is not null)
            return existing;

        var payable = BuildFromRequest(request, createdBy);
        await _repo.AddAsync(payable, ct);
        await _repo.SaveChangesAsync(ct);
        return payable;
    }

    public async Task<AccountsPayable> StageFromOriginAsync(
        CreateAccountsPayableFromOriginRequest request,
        Guid createdBy,
        CancellationToken ct = default
    )
    {
        var existing = await _repo.GetByOriginAsync(
            request.TenantId,
            request.CompanyId,
            request.OriginType,
            request.OriginId,
            ct
        );
        if (existing is not null)
            return existing;

        var payable = BuildFromRequest(request, createdBy);
        await _repo.AddAsync(payable, ct);

        return payable;
    }

    private static AccountsPayable BuildFromRequest(
        CreateAccountsPayableFromOriginRequest request,
        Guid createdBy
    )
    {
        var payable = AccountsPayable.CreateFromOrigin(
            request.TenantId,
            request.CompanyId,
            request.BranchId,
            request.SupplierId,
            request.OriginType,
            request.OriginId,
            request.DocumentType,
            request.DocumentNumber,
            request.IssueDate,
            request.AccountingDate,
            createdBy
        );
        foreach (var installment in request.Installments)
            payable.AddInstallment(installment.InstallmentNumber, installment.DueDate, installment.Amount);

        return payable;
    }
}

using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;

namespace ERP.Application.Modules.Payables.UseCases;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 — payload para crear una CxP genérica a partir de un documento de
/// origen (Compra, Gasto o manual). No es un <c>IRequest</c> de MediatR: es un caso de uso interno,
/// invocado directamente por el módulo de origen (hoy solo Gastos, ver
/// <c>ConfirmExpenseDocumentHandler</c>) después de que su propio flujo (posting incluido) ya tuvo
/// éxito — nunca expuesto vía API en esta fase (ver ticket: "dejar API para fase siguiente").
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
    DateOnly DueDate,
    decimal TotalAmount
);

public interface IAccountsPayableService
{
    /// <summary>
    /// Idempotente por (TenantId, CompanyId, OriginType, OriginId): si ya existe una CxP para ese
    /// origen, la devuelve tal cual (nunca crea un duplicado, nunca falla por "ya existe" — un
    /// reintento del módulo de origen, p. ej. tras un timeout de red, debe ser seguro). Por ahora
    /// genera una única cuota por el total (<paramref name="request"/>.TotalAmount,
    /// <paramref name="request"/>.DueDate) — cronogramas multi-cuota quedan para cuando el origen
    /// los necesite, sin cambios en <see cref="AccountsPayable.AddInstallment"/> (ya es genérico).
    /// </summary>
    Task<AccountsPayable> CreateFromOriginAsync(
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
        payable.AddInstallment(1, request.DueDate, request.TotalAmount);

        await _repo.AddAsync(payable, ct);
        await _repo.SaveChangesAsync(ct);

        return payable;
    }
}

using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;

namespace ERP.Domain.Modules.Payables.Interfaces;

public interface IAccountsPayableRepository
{
    Task<AccountsPayable?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Natural key real (uq_accounts_payables_tenant_company_origin) — usado por
    /// <c>AccountsPayableService.CreateFromOriginAsync</c> para la idempotencia: nunca crear un
    /// segundo <see cref="AccountsPayable"/> para el mismo documento de origen.
    /// </summary>
    Task<AccountsPayable?> GetByOriginAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType originType,
        Guid originId,
        CancellationToken ct = default
    );

    /// <summary>
    /// PAYABLES-PURCHASE-MIGRATION-10 — descubrimiento mínimo, sin tracking, del
    /// <see cref="AccountsPayable.OriginId"/> de una CxP (reemplaza
    /// <c>IPurchasePayableRepository.GetPurchaseInvoiceIdAsync</c>), usado únicamente para
    /// determinar qué Lock A adquirir ANTES de la recarga autoritativa. Deliberadamente no rastrea
    /// la entidad — así la posterior llamada a <see cref="GetByIdAsync"/> (ya tracking) ejecutada
    /// después del lock garantiza una lectura fresca real desde PostgreSQL.
    /// </summary>
    Task<Guid?> GetOriginIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Listado paginado filtrado por origen (p. ej. solo <c>PurchaseInvoice</c>, para
    /// <c>PurchasePayablesController</c>) — necesario para que el usuario consulte/seleccione qué
    /// cuenta por pagar liquidar antes de registrar un pago.
    /// </summary>
    Task<(IReadOnlyList<AccountsPayable> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType originType,
        AccountsPayableStatus? status,
        Guid? supplierId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// PAYABLES-READ-API-11 — listado paginado genérico (cualquier origen) para
    /// <c>PayablesController</c>, a diferencia de <see cref="GetPagedAsync"/> (que exige un
    /// <see cref="AccountsPayableOriginType"/> fijo, usado por <c>PurchasePayablesController</c>).
    /// <paramref name="search"/> filtra por <c>DocumentNumber</c> o nombre del proveedor
    /// (razón social/comercial); <paramref name="dueDateFrom"/>/<paramref name="dueDateTo"/>
    /// filtran por el vencimiento más próximo entre las cuotas (<c>Installments.Min(DueDate)</c>).
    /// </summary>
    Task<(IReadOnlyList<AccountsPayable> Items, int Total)> SearchAsync(
        Guid tenantId,
        Guid companyId,
        AccountsPayableOriginType? originType,
        AccountsPayableStatus? status,
        Guid? supplierId,
        DateOnly? dueDateFrom,
        DateOnly? dueDateTo,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task AddAsync(AccountsPayable payable, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

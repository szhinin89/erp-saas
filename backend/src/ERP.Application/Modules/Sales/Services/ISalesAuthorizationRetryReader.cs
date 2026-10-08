using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>Read committed authorization after a persistence conflict, in a fresh context.</summary>
public interface ISalesAuthorizationRetryReader
{
    bool IsConflict(Exception exception);
    Task<CommittedSalesAuthorization?> ReadAuthorizedAsync(
        Guid tenantId, Guid companyId, Guid branchId, Guid invoiceId, CancellationToken ct);
}

public sealed record CommittedSalesAuthorization(
    SalesInvoice Invoice, ElectronicDocument? ElectronicDocument);

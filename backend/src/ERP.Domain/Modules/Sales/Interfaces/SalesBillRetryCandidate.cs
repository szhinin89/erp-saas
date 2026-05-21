namespace ERP.Domain.Modules.Sales.Interfaces;

public sealed record SalesBillRetryCandidate(Guid BillId, Guid SubscriberId);

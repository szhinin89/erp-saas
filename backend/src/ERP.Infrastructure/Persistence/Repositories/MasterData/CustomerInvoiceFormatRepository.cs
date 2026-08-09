using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class CustomerInvoiceFormatRepository
    : ClassificationCatalogRepositoryBase<CustomerInvoiceFormat>,
        ICustomerInvoiceFormatRepository
{
    public CustomerInvoiceFormatRepository(ErpDbContext db)
        : base(db) { }
}

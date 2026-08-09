using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class CustomerCategoryRepository
    : ClassificationCatalogRepositoryBase<CustomerCategory>,
        ICustomerCategoryRepository
{
    public CustomerCategoryRepository(ErpDbContext db)
        : base(db) { }
}

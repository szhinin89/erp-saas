using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class CustomerClassificationRepository
    : ClassificationCatalogRepositoryBase<CustomerClassification>,
        ICustomerClassificationRepository
{
    public CustomerClassificationRepository(ErpDbContext db)
        : base(db) { }
}

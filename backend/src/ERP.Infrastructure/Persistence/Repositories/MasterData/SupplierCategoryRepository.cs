using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class SupplierCategoryRepository
    : ClassificationCatalogRepositoryBase<SupplierCategory>,
        ISupplierCategoryRepository
{
    public SupplierCategoryRepository(ErpDbContext db)
        : base(db) { }
}

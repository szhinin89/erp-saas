using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class SupplierTypeRepository
    : ClassificationCatalogRepositoryBase<SupplierType>,
        ISupplierTypeRepository
{
    public SupplierTypeRepository(ErpDbContext db)
        : base(db) { }
}

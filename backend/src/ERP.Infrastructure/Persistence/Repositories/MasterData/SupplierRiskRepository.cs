using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class SupplierRiskRepository
    : ClassificationCatalogRepositoryBase<SupplierRisk>,
        ISupplierRiskRepository
{
    public SupplierRiskRepository(ErpDbContext db)
        : base(db) { }
}

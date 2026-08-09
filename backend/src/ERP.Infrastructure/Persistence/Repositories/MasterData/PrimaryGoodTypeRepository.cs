using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class PrimaryGoodTypeRepository
    : ClassificationCatalogRepositoryBase<PrimaryGoodType>,
        IPrimaryGoodTypeRepository
{
    public PrimaryGoodTypeRepository(ErpDbContext db)
        : base(db) { }
}

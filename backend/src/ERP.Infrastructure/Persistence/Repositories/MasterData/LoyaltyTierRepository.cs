using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class LoyaltyTierRepository
    : ClassificationCatalogRepositoryBase<LoyaltyTier>,
        ILoyaltyTierRepository
{
    public LoyaltyTierRepository(ErpDbContext db)
        : base(db) { }
}

using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

public sealed class SupplierRatingRepository
    : ClassificationCatalogRepositoryBase<SupplierRating>,
        ISupplierRatingRepository
{
    public SupplierRatingRepository(ErpDbContext db)
        : base(db) { }
}

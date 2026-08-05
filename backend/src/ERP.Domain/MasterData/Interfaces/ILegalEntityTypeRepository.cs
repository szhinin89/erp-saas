namespace ERP.Domain.MasterData.Interfaces;

public interface ILegalEntityTypeRepository
{
    Task<bool> ExistsActiveAsync(int code, CancellationToken cancellationToken = default);
}

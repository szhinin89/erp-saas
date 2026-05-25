namespace ERP.Application.Common;

public interface IUnitOfWork
{
    /// <summary>True when the shared DbContext already participates in an open database transaction.</summary>
    bool HasActiveTransaction { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void ClearChangeTracker();
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

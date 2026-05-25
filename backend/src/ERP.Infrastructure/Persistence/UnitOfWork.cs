using Microsoft.EntityFrameworkCore.Storage;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ErpDbContext _context;
    private readonly IUnifiedDocumentSync _documentSync;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ErpDbContext context, IUnifiedDocumentSync documentSync)
    {
        _context = context;
        _documentSync = documentSync;
    }

    public bool HasActiveTransaction =>
        _transaction is not null || _context.Database.CurrentTransaction is not null;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        await _documentSync.FlushAsync(ct);
        return await _context.SaveChangesAsync(ct);
    }

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (HasActiveTransaction)
            return;

        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}

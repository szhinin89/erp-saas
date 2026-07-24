using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ERP.Application.Common;

namespace ERP.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ErpDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ErpDbContext context)
    {
        _context = context;
    }

    public bool HasActiveTransaction =>
        _transaction is not null || _context.Database.CurrentTransaction is not null;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (HasActiveTransaction)
            return;

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        // CreateExecutionStrategy() es obligatorio aquí: si el proveedor tiene reintentos
        // automáticos habilitados (p. ej. Npgsql EnableRetryOnFailure), EF Core lanza en runtime si
        // se abre una transacción manual fuera de una estrategia de ejecución. Hoy este proyecto no
        // habilita reintentos, pero este wrapper deja el código correcto para cuando se habiliten,
        // sin tener que tocar cada caso de uso que haga transacciones manuales.
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await operation(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}

namespace ERP.Application.Common;

public interface IUnitOfWork
{
    /// <summary>True when the shared DbContext already participates in an open database transaction.</summary>
    bool HasActiveTransaction { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearChangeTracker();
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="operation"/> como una única unidad atómica: abre una transacción de
    /// BD (envuelta en el <c>IExecutionStrategy</c> de EF Core — obligatorio para transacciones
    /// manuales cuando el proveedor tiene reintentos automáticos habilitados), corre la operación,
    /// hace un único <c>SaveChangesAsync</c> final y confirma. Si <paramref name="operation"/> o el
    /// <c>SaveChangesAsync</c> lanzan cualquier excepción, hace rollback completo antes de
    /// relanzarla — no puede quedar ningún dato parcial persistido. Preferir este método sobre
    /// Begin/Commit/Rollback manual para cualquier caso de uso que deba ser todo-o-nada.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    );
}

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — obligaciones creadas en el request actual (scoped), pendientes de su
/// intento inicial tras el commit. Solo en memoria: si el proceso termina antes del intento, la
/// obligación ya persistida queda Pending y se resuelve por reproceso manual. No es una cola de
/// reintentos.
/// </summary>
public sealed class AccountingPostingDispatchQueue
{
    private readonly List<Guid> _ids = [];

    public void Enqueue(Guid postingId)
    {
        if (!_ids.Contains(postingId))
            _ids.Add(postingId);
    }

    public IReadOnlyList<Guid> Drain()
    {
        var drained = _ids.ToList();
        _ids.Clear();
        return drained;
    }
}

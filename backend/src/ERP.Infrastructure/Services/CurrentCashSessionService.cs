using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Contexto operativo POS (ADR — Rediseño del módulo de Caja, Fase 3). A diferencia de
/// <see cref="CurrentBranchService"/>/<see cref="CurrentCompanyService"/> (lectura pura de
/// header/claim, sin I/O), esta implementación necesita resolver la <c>CashSession</c> abierta
/// contra base de datos — no hay forma de exponer <c>Guid?</c> síncronos sin una llamada
/// bloqueante puntual. Se resuelve una única vez por instancia (memoización en campo privado) y
/// se usa el mismo patrón sync-over-async ya aceptado en este proyecto para código Infrastructure
/// que debe exponer una API síncrona (ver <c>ErpDbContext.SaveChanges</c>, que bloquea sobre
/// <c>SaveChangesAsync</c> de la misma forma) — en Kestrel/ASP.NET Core no hay
/// <c>SynchronizationContext</c>, por lo que este bloqueo no arriesga deadlock.
/// </summary>
public sealed class CurrentCashSessionService : ICurrentCashSession
{
    private readonly ICashSessionRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ICurrentBranch _branch;

    private bool _resolved;
    private CashSession? _session;

    public CurrentCashSessionService(
        ICashSessionRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user,
        ICurrentBranch branch)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
        _branch = branch;
    }

    /// <summary>
    /// Cache por request: esta clase se registra Scoped (una instancia por request, igual que
    /// ICurrentBranch/ICurrentCompany/ICurrentUser), así que memoizar en un campo de instancia
    /// logra exactamente el mismo alcance temporal que HttpContext.Items sin depender de
    /// IHttpContextAccessor ni introducir una clave de caché nueva que mantener sincronizada —
    /// se evaluó HttpContext.Items y se descartó por redundante frente al lifetime ya scoped.
    /// </summary>
    private CashSession? ResolveSession()
    {
        if (_resolved)
            return _session;

        _resolved = true;

        if (!_user.IsAuthenticated || _user.UserId == Guid.Empty)
            return _session = null;

        var session = _repo
            .GetOpenByUserAsync(_tenant.TenantId, _user.UserId, CancellationToken.None)
            .GetAwaiter().GetResult();

        // La sesión pertenece al usuario autenticado y está Open (ya filtrado por el repositorio).
        // Falta validar que corresponda a la sucursal activa: si el usuario cambió de sucursal sin
        // cerrar la caja, esa sesión no es válida en el contexto operativo actual — se trata como
        // "sin sesión abierta", nunca se expone entre sucursales.
        if (session is not null && session.BranchId != _branch.BranchId)
            session = null;

        return _session = session;
    }

    public Guid? CashSessionId => ResolveSession()?.Id;
    public Guid? CashRegisterId => ResolveSession()?.CashRegisterId;
    public Guid? EmissionPointId => ResolveSession()?.EmissionPointId;
    public Guid? BranchId => ResolveSession()?.BranchId;
    public bool HasOpenSession => ResolveSession() is not null;
    public string? CashRegisterCodeSnapshot => ResolveSession()?.CashRegisterCodeSnapshot;
    public string? CashRegisterNameSnapshot => ResolveSession()?.CashRegisterNameSnapshot;
}

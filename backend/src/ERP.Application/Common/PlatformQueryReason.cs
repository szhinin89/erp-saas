namespace ERP.Application.Common;

/// <summary>Motivo documentado para consultar sin el filtro global de suscriptor.</summary>
public enum PlatformQueryReason
{
    SubscriberScopedExplicit,
    CrossSubscriberSystem,
    PlatformMetrics,
    BackgroundJob,
    Seeding,
    DbContextSync,
    DevOnly,
}

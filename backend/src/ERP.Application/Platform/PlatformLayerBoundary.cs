namespace ERP.Application.Platform;

/// <summary>
/// Marcador de capa Platform (SaaS). Los casos de uso viven hoy bajo carpetas legacy;
/// el namespace lógico objetivo es <c>ERP.Application.Platform.*</c>.
/// </summary>
/// <remarks>
/// Mapeo físico → lógico (sin mover archivos aún):
/// <list type="bullet">
///   <item><c>Modules/Platform/Companies</c> → Platform.Companies</item>
///   <item><c>Modules/Access/UseCases/SuperAdminSubscribers</c> → Platform.Subscribers</item>
///   <item><c>Modules/Tenants</c> (settings, subscription) → Platform.Subscribers</item>
///   <item><c>Subscriptions/*</c> → Platform.Billing / Platform.Plans</item>
/// </list>
/// API canónica: <c>/api/platform/*</c>. Ver docs/ARCHITECTURE.md.
/// </remarks>
public static class PlatformLayerBoundary;

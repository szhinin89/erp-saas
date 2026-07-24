namespace ERP.Application.ERPRuntime;

/// <summary>
/// Marcador de capa ERP Runtime (operación fiscal por <c>company_id</c>).
/// </summary>
/// <remarks>
/// Mapeo físico → lógico:
/// <list type="bullet">
///   <item><c>Modules/Sales</c>, <c>Sales/*</c> → ERPRuntime.Sales</item>
///   <item><c>Modules/Inventory</c>, <c>Inventory/*</c> → ERPRuntime.Inventory</item>
///   <item><c>Modules/Purchasing</c> → ERPRuntime.Purchasing</item>
///   <item><c>Modules/Accounting</c> → ERPRuntime.Accounting</item>
/// </list>
/// Requiere JWT con <c>company_id</c> + <see cref="Behaviors.CompanyScopeBehavior{TRequest,TResponse}"/>.
/// </remarks>
public static class ErpRuntimeLayerBoundary;

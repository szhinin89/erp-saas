using ERP.Domain.Modules.Pricing.Entities;

namespace ERP.Domain.Modules.Pricing.Interfaces;

/// <summary>Motivo por el que no se pudo asignar la lista predeterminada.</summary>
public enum PriceListDefaultAssignmentError
{
    None = 0,
    NotFound = 1,
    Inactive = 2,
}

/// <summary>
/// Resultado de <see cref="IPriceListDefaultAssigner.AssignAsync"/>. <see cref="Changed"/> indica
/// si alguna bandera <c>IsDefault</c> cambió (y por tanto si el llamador debe persistir).
/// </summary>
public sealed record PriceListDefaultAssignment(
    PriceListDefaultAssignmentError Error,
    PriceList? Default,
    bool Changed
)
{
    public bool IsSuccess => Error == PriceListDefaultAssignmentError.None;
}

/// <summary>Mensajes únicos de los rechazos de <see cref="IPriceListDefaultAssigner"/>.</summary>
public static class PriceListDefaultMessages
{
    public const string NotFound = "Lista de precios no encontrada.";
    public const string Inactive =
        "No se puede marcar como predeterminada una lista deshabilitada.";
}

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01: única lógica de cambio de <see cref="PriceList.IsDefault"/>
/// desde la reasignación explícita del default (PATCH <c>set-default</c> y Configuración → Ventas).
/// Garantiza como máximo una lista predeterminada por empresa operativa (o ninguna con
/// <c>priceListId = null</c>): desmarca toda lista que conserve la bandera — activa o no, porque
/// <c>uq_price_lists_tenant_company_default</c> no filtra por estado — y registra cada cambio en
/// el historial de configuración. Exige una transacción abierta por el llamador: puede enviar
/// cambios intermedios a la BD (el índice único no es diferible), pero el commit o rollback
/// completo es del llamador, de modo que el cambio viaja atómicamente con el resto de su operación.
/// Valida la lista destino antes de mutar nada: un rechazo no deja cambios pendientes.
/// </summary>
public interface IPriceListDefaultAssigner
{
    Task<PriceListDefaultAssignment> AssignAsync(
        Guid tenantId,
        Guid? priceListId,
        Guid changedBy,
        CancellationToken ct = default
    );
}

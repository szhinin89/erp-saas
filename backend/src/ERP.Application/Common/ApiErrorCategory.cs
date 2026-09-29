namespace ERP.Application.Common;

/// <summary>
/// Categoría oficial de un código de error (ADR-027 §8). Cada código de error de
/// <see cref="ApiResponseCodes"/> declara exactamente una categoría junto a su entrada en
/// <see cref="MessageCatalog"/>; el HTTP status se deriva de la categoría en un único punto
/// (<c>ERP.API.Extensions.ApiErrorStatus</c>, ADR-027 §9) — nunca por código, controller ni módulo.
/// Application no conoce HTTP: esta enumeración solo clasifica.
/// </summary>
public enum ApiErrorCategory
{
    /// <summary>Datos/regla de validación rechazados (FluentValidation, <c>Result.ValidationFailure</c>).</summary>
    Validation,

    /// <summary>Petición válida en forma que el estado de negocio no permite ejecutar.</summary>
    BusinessRule,

    /// <summary>Conflicto con el estado actual: unicidad, concurrencia optimista, estado cambiado.</summary>
    Duplicate,

    /// <summary>Recurso inexistente o fuera de alcance (tenant/empresa/sucursal) — mismo trato.</summary>
    NotFound,

    /// <summary>Sin sesión válida.</summary>
    Authentication,

    /// <summary>Sesión válida sin permiso para el recurso, la empresa o la sucursal.</summary>
    Authorization,

    /// <summary>Indisponibilidad técnica interna (base de datos).</summary>
    Infrastructure,

    /// <summary>Falla de un sistema externo (SRI).</summary>
    Integration,

    /// <summary>Límite de tasa excedido.</summary>
    RateLimit,

    /// <summary>Última red de seguridad — nunca el resultado esperado de una condición conocida.</summary>
    InternalError,
}

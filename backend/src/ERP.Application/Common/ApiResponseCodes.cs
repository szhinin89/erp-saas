namespace ERP.Application.Common;

/// <summary>
/// Códigos públicos y estables del campo <c>code</c> del envelope de respuesta
/// (<c>ApiResponse&lt;T&gt;</c>). Formato SCREAMING_SNAKE_CASE. Fuente única de
/// verdad: <see cref="MessageCatalog"/> deriva <c>severity</c> y los mensajes
/// (<c>message.user</c> / <c>message.dev</c>) a partir de estos valores.
/// </summary>
/// <remarks>
/// Organización por dominio: los códigos transversales viven en
/// <see cref="Common"/>. Nuevos módulos del ERP (Items, Clientes, Proveedores,
/// Inventario, Ventas, Compras, Contabilidad, ...) agregan su propia clase
/// estática anidada (p. ej. <c>ApiResponseCodes.Inventory</c>) con sus
/// constantes <c>SCREAMING_SNAKE_CASE</c>. Cada código nuevo requiere una
/// entrada en <see cref="MessageCatalog"/>: la regla de arquitectura
/// <c>MessageCatalog_has_exactly_one_entry_per_ApiResponseCode</c> recorre
/// <see cref="ApiResponseCodes"/> y todas sus clases anidadas, por lo que la
/// cobertura se exige automáticamente sin tocar el test.
/// </remarks>
public static class ApiResponseCodes
{
    /// <summary>Códigos transversales (éxito genérico, errores HTTP comunes, infraestructura).</summary>
    public static class Common
    {
        public const string Ok = "OK";
        public const string Created = "CREATED";
        public const string ValidationError = "VALIDATION_ERROR";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string UniqueViolation = "UNIQUE_VIOLATION";
        public const string Forbidden = "FORBIDDEN";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string SriCommunicationError = "SRI_COMMUNICATION_ERROR";
        public const string CompanyScopeForbidden = "COMPANY_SCOPE_FORBIDDEN";
        public const string BranchScopeForbidden = "BRANCH_SCOPE_FORBIDDEN";
        public const string CompanyRucAlreadyExists = "COMPANY_RUC_ALREADY_EXISTS";
        public const string BadRequest = "BAD_REQUEST";
        public const string InternalError = "INTERNAL_ERROR";
        public const string RateLimited = "RATE_LIMITED";
    }
}

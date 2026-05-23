namespace ERP.Application.Common;

/// <summary>Códigos estables para mapeo HTTP en <see cref="ERP.API.Extensions.ApiResultExtensions"/>.</summary>
public static class ResultErrorCodes
{
    public const string UniqueViolation     = "unique_violation";
    public const string Conflict            = "conflict";
    public const string ValidationFailure   = "validation_failure";
}

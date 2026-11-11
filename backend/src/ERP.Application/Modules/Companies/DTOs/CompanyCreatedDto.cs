namespace ERP.Application.Modules.Companies.DTOs;

/// <summary>
/// Credenciales del administrador inicial. <c>TemporaryPassword</c> existe SOLO en la respuesta de
/// creación (y en la de regeneración): nunca se persiste ni aparece en ninguna consulta posterior
/// de empresas; el administrador debe cambiarla en su primer ingreso.
/// </summary>
public sealed record InitialAdministratorCredentialsDto(
    Guid IdentityUserId,
    string Username,
    string TemporaryPassword
);

/// <summary>
/// Respuesta de <c>POST /api/v1/companies</c>: los mismos campos de <see cref="CompanyDetailDto"/>
/// (contrato histórico intacto) más, cuando se registró un administrador inicial, sus credenciales.
/// </summary>
public sealed record CompanyCreatedDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    InitialAdministratorCredentialsDto? InitialAdministrator
)
{
    public static CompanyCreatedDto From(
        CompanyDetailDto c,
        InitialAdministratorCredentialsDto? administrator
    ) =>
        new(
            c.Id,
            c.TenantId,
            c.LegalName,
            c.TradeName,
            c.TaxId,
            c.CountryCode,
            c.Timezone,
            c.CurrencyCode,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt,
            administrator
        );
}

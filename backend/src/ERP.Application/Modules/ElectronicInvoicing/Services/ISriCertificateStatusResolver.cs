using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Modules.ElectronicInvoicing.Services;

/// <summary>Resultado de resolver el estado del certificado de una configuración SRI ya persistida.</summary>
public sealed record SriCertificateStatus(
    bool Installed,
    bool PasswordCorrect,
    bool Valid,
    DateTime? NotAfterUtc,
    string? Subject,
    string? Issuer,
    string? ErrorMessage);

/// <summary>
/// Resuelve si el certificado de una empresa está instalado y es válido (contraseña correcta,
/// vigente) — carga bytes vía <see cref="Application.Common.Interfaces.IFileStorage"/> y usa
/// <see cref="Application.Common.Interfaces.SRI.ISriCertificateInspector"/>. Único punto de esta
/// lógica: la consumen tanto el diagnóstico completo (ValidateSriConfiguration) como el status
/// liviano (GetElectronicInvoicingStatus) — evita reimplementarla en cada handler.
/// </summary>
public interface ISriCertificateStatusResolver
{
    Task<SriCertificateStatus> ResolveAsync(SriSettings settings, CancellationToken cancellationToken = default);
}

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Certificado digital .p12 para firma del XML de comprobantes electrónicos.
/// La contraseña se almacena CIFRADA a nivel de aplicación (AES-256). Nunca en texto plano.
/// </summary>
public class DigitalCertificate
{
    public Guid     Id           { get; set; }
    public Guid     CompanyId    { get; set; }
    public string   FilePath     { get; set; } = null!;
    public string   PasswordHash { get; set; } = null!;
    public string?  OwnerName    { get; set; }
    public DateOnly? IssuedAt    { get; set; }
    public DateOnly  ExpiresAt   { get; set; }
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; }

    public Company Company { get; set; } = null!;
}

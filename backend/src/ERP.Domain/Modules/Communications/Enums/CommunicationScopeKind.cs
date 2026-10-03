namespace ERP.Domain.Modules.Communications.Enums;

/// <summary>
/// ADR-039 D3 — <see cref="Company"/>: comunicación de una empresa (tenant + empresa obligatorios,
/// perfil SMTP de la empresa). <see cref="System"/>: comunicación de la instalación (sin
/// tenant/empresa, perfil de instancia), p. ej. recuperación de contraseña.
/// </summary>
public enum CommunicationScopeKind
{
    System = 1,
    Company = 2,
}

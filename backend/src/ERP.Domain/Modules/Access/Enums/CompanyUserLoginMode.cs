namespace ERP.Domain.Access.Enums;

public enum CompanyUserLoginMode
{
    /// <summary>El usuario debe elegir la sucursal en cada inicio de sesión.</summary>
    AskBranch = 0,

    /// <summary>El login ingresa directamente a la sucursal por defecto de <c>CompanyUserPreferences</c>.</summary>
    DirectToDefault = 1,
}

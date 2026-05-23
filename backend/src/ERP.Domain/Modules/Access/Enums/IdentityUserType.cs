namespace ERP.Domain.Access.Enums;

public enum IdentityUserType
{
    /// <summary>Operador de plataforma (SuperAdmin global). Sin subscriber operativo.</summary>
    Platform = 0,

    /// <summary>
    /// Reservado — no implementado. No usar en authorization guards ni policies.
    /// La relación usuario→subscriber se resuelve vía CompanyUserMembership → Company → Subscriber.
    /// </summary>
    Subscriber = 1,

    /// <summary>Usuario operativo ERP. Acceso a empresas vía <c>CompanyUserMembership</c>.</summary>
    Company = 2,
}

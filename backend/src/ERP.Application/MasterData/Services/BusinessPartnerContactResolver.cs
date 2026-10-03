using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.MasterData.Services;

/// <summary>
/// Regla única para obtener el Email/Address de contacto de un tercero (BP V2). BusinessPartner no
/// contiene estos datos directamente: Email vive en <c>BusinessPartnerContact.Contact.Email</c>
/// (contacto primario activo) y Address en <c>BusinessPartnerLocation.Address.AddressLine</c>
/// (ubicación primaria activa), con fallback al Email de la ubicación primaria si el contacto no lo
/// tiene. Nunca inventa datos: si no existen, ambos quedan null.
/// <para>
/// Origen: SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (snapshot del cliente en la factura). Movido aquí
/// en ZH-EDOC-COMMUNICATIONS-01 para que Retenciones resuelva el correo del sujeto retenido con la
/// MISMA regla, sin duplicarla.
/// </para>
/// </summary>
public static class BusinessPartnerContactResolver
{
    public static async Task<(string? Email, string? Address)> ResolveAsync(
        IBusinessPartnerContactRepository contactRepo,
        IBusinessPartnerLocationRepository locationRepo,
        Guid businessPartnerId,
        CancellationToken ct
    )
    {
        var contacts = await contactRepo.GetByBusinessPartnerAsync(businessPartnerId, true, ct);
        var primaryContact = contacts.FirstOrDefault(c => c.IsPrimary) ?? contacts.FirstOrDefault();
        var email = primaryContact?.Contact.Email;

        var locations = await locationRepo.GetByBusinessPartnerAsync(businessPartnerId, true, ct);
        var primaryLocation = locations.FirstOrDefault(l => l.IsPrimary) ?? locations.FirstOrDefault();
        var address = primaryLocation?.Address.AddressLine;
        if (string.IsNullOrWhiteSpace(email))
            email = primaryLocation?.Email;

        return (email, address);
    }
}

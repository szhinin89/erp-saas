using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;

namespace ERP.Application.MasterData.DTOs;

public sealed record BpLocationDto(
    Guid Id,
    Guid BusinessPartnerId,
    string Name,
    string LocationType,
    string TypeLabel,
    string[] Purposes,
    string AddressLine,
    string? ProvinceCode,
    string? CantonCode,
    string? ParishCode,
    string? Phone,
    string? Email,
    string? OtherDescription,
    bool IsPrimary,
    bool IsActive,
    DateTime CreatedAt)
{
    public static BpLocationDto From(BusinessPartnerLocation l) => new(
        l.Id,
        l.BusinessPartnerId,
        l.Name,
        l.Type.ToString(),
        BpDtoHelpers.LocationTypeLabel(l.Type),
        BpDtoHelpers.ParsePurposes(l.Purpose),
        l.Address.AddressLine,
        l.Address.ProvinceCode,
        l.Address.CantonCode,
        l.Address.ParishCode,
        l.Phone,
        l.Email,
        l.OtherDescription,
        l.IsPrimary,
        l.IsActive,
        l.CreatedAt);
}

public sealed record BpContactDto(
    Guid Id,
    Guid BusinessPartnerId,
    Guid? LocationId,
    string FirstName,
    string? LastName,
    string FullName,
    string? Position,
    string ContactRole,
    string RoleLabel,
    string? OtherDescription,
    string? Phone,
    string? Mobile,
    string? Email,
    string? Notes,
    bool IsPrimary,
    bool IsActive,
    DateTime CreatedAt)
{
    public static BpContactDto From(BusinessPartnerContact c) => new(
        c.Id,
        c.BusinessPartnerId,
        c.LocationId,
        c.FirstName,
        c.LastName,
        c.FullName,
        c.Position,
        c.Role.ToString(),
        BpDtoHelpers.ContactRoleLabel(c.Role),
        c.OtherDescription,
        c.Contact.Phone,
        c.Contact.Mobile,
        c.Contact.Email,
        c.Notes,
        c.IsPrimary,
        c.IsActive,
        c.CreatedAt);
}

// ── Helpers estáticos fuera de los records (evita conflicto nombre propiedad vs tipo enum) ──

internal static class BpDtoHelpers
{
    internal static string LocationTypeLabel(LocationType t) => t switch
    {
        LocationType.Matrix => "Matriz",
        LocationType.Branch => "Sucursal",
        LocationType.Office => "Oficina",
        LocationType.Warehouse => "Bodega",
        LocationType.DeliveryPoint => "Punto de entrega",
        _ => "Otro",
    };

    internal static string[] ParsePurposes(LocationPurpose p)
    {
        var list = new List<string>();
        if (p.HasFlag(LocationPurpose.Billing)) list.Add("Facturación");
        if (p.HasFlag(LocationPurpose.Delivery)) list.Add("Entrega");
        if (p.HasFlag(LocationPurpose.Fiscal)) list.Add("Fiscal");
        if (p.HasFlag(LocationPurpose.Correspondence)) list.Add("Correspondencia");
        return list.ToArray();
    }

    internal static string ContactRoleLabel(ContactRole r) => r switch
    {
        ContactRole.Commercial => "Comercial",
        ContactRole.Accounting => "Contabilidad",
        ContactRole.Management => "Gerencia",
        ContactRole.Reception => "Recepción",
        ContactRole.Dispatch => "Despacho",
        ContactRole.Billing => "Facturación",
        ContactRole.Technical => "Técnico",
        ContactRole.Purchasing => "Compras",
        ContactRole.Legal => "Legal",
        _ => "Otro",
    };
}

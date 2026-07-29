using ERP.Domain.Common;

namespace ERP.Domain.Branches.Entities;

/// <summary>Sucursal operativa del ERP: ubicación física o administrativa de la empresa (Identidad, Dirección, Contacto, Responsable, Operación).</summary>
public sealed class Branch : MasterEntity, ICompanyOperationalEntity
{
    /// <summary>Empresa a la que pertenece esta sucursal. Null = sucursal creada antes de P1-1.</summary>
    public Guid CompanyId { get; private set; }

    // Identificación
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsMainBranch { get; private set; }

    // Dirección
    public string Address { get; private set; } = null!;
    public string? CountryId { get; private set; }
    public string? ProvinceId { get; private set; }
    public string? CantonId { get; private set; }
    public string? ParishId { get; private set; }
    public string? Reference { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Latitude { get; private set; }
    public string? Longitude { get; private set; }

    // Contacto
    public string? Phone { get; private set; }
    public string? SecondaryPhone { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }

    // Responsable (información libre, sin vínculo a empleados/usuarios)
    public string? ManagerName { get; private set; }
    public string? ManagerPosition { get; private set; }
    public string? ManagerEmail { get; private set; }
    public string? ManagerPhone { get; private set; }

    // Operación
    public DateOnly? OpeningDate { get; private set; }
    public string? InternalNotes { get; private set; }

    private Branch() { }

    public static Branch Create(
        Guid tenantId,
        string name,
        string address,
        string code,
        string? description,
        string? reference,
        string? postalCode,
        string? phone,
        string? secondaryPhone,
        string? email,
        string? website,
        string? managerName,
        string? managerPosition,
        string? managerEmail,
        string? managerPhone,
        string? countryId,
        string? provinceId,
        string? cantonId,
        string? parishId,
        string? latitude,
        string? longitude,
        DateOnly? openingDate,
        string? internalNotes,
        bool isMainBranch,
        Guid createdBy,
        Guid companyId = default
    )
    {
        var b = new Branch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Name = name.Trim(),
            Address = address.Trim(),
            Code = code,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            SecondaryPhone = string.IsNullOrWhiteSpace(secondaryPhone)
                ? null
                : secondaryPhone.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim(),
            ManagerName = string.IsNullOrWhiteSpace(managerName) ? null : managerName.Trim(),
            ManagerPosition = string.IsNullOrWhiteSpace(managerPosition)
                ? null
                : managerPosition.Trim(),
            ManagerEmail = string.IsNullOrWhiteSpace(managerEmail) ? null : managerEmail.Trim(),
            ManagerPhone = string.IsNullOrWhiteSpace(managerPhone) ? null : managerPhone.Trim(),
            CountryId = string.IsNullOrWhiteSpace(countryId) ? null : countryId.Trim(),
            ProvinceId = string.IsNullOrWhiteSpace(provinceId) ? null : provinceId.Trim(),
            CantonId = string.IsNullOrWhiteSpace(cantonId) ? null : cantonId.Trim(),
            ParishId = string.IsNullOrWhiteSpace(parishId) ? null : parishId.Trim(),
            Latitude = string.IsNullOrWhiteSpace(latitude) ? null : latitude.Trim(),
            Longitude = string.IsNullOrWhiteSpace(longitude) ? null : longitude.Trim(),
            OpeningDate = openingDate,
            InternalNotes = string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes.Trim(),
            IsMainBranch = isMainBranch,
        };
        b.SetCreated(createdBy);
        return b;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Sucursal Principal), ver política de Bootstrap en
    /// <c>CLAUDE.md</c>. <see cref="Update"/> permanece abierto: es el único camino para que el
    /// administrador complete la información propia de la empresa (Tipo B) sobre esta sucursal.
    /// </summary>
    public static Branch CreateSystemSeeded(
        Guid tenantId,
        string name,
        string address,
        string code,
        string? description,
        string? reference,
        string? postalCode,
        string? phone,
        string? secondaryPhone,
        string? email,
        string? website,
        string? managerName,
        string? managerPosition,
        string? managerEmail,
        string? managerPhone,
        string? countryId,
        string? provinceId,
        string? cantonId,
        string? parishId,
        string? latitude,
        string? longitude,
        DateOnly? openingDate,
        string? internalNotes,
        bool isMainBranch,
        Guid createdBy,
        Guid companyId = default
    )
    {
        var b = Create(
            tenantId,
            name,
            address,
            code,
            description,
            reference,
            postalCode,
            phone,
            secondaryPhone,
            email,
            website,
            managerName,
            managerPosition,
            managerEmail,
            managerPhone,
            countryId,
            provinceId,
            cantonId,
            parishId,
            latitude,
            longitude,
            openingDate,
            internalNotes,
            isMainBranch,
            createdBy,
            companyId
        );
        b.MarkAsSystemSeeded();
        return b;
    }

    public void Update(
        string name,
        string address,
        string? description,
        string? reference,
        string? postalCode,
        string? phone,
        string? secondaryPhone,
        string? email,
        string? website,
        string? managerName,
        string? managerPosition,
        string? managerEmail,
        string? managerPhone,
        string? countryId,
        string? provinceId,
        string? cantonId,
        string? parishId,
        string? latitude,
        string? longitude,
        DateOnly? openingDate,
        string? internalNotes,
        bool isMainBranch,
        Guid updatedBy
    )
    {
        Name = name.Trim();
        Address = address.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        SecondaryPhone = string.IsNullOrWhiteSpace(secondaryPhone) ? null : secondaryPhone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        ManagerName = string.IsNullOrWhiteSpace(managerName) ? null : managerName.Trim();
        ManagerPosition = string.IsNullOrWhiteSpace(managerPosition)
            ? null
            : managerPosition.Trim();
        ManagerEmail = string.IsNullOrWhiteSpace(managerEmail) ? null : managerEmail.Trim();
        ManagerPhone = string.IsNullOrWhiteSpace(managerPhone) ? null : managerPhone.Trim();
        CountryId = string.IsNullOrWhiteSpace(countryId) ? null : countryId.Trim();
        ProvinceId = string.IsNullOrWhiteSpace(provinceId) ? null : provinceId.Trim();
        CantonId = string.IsNullOrWhiteSpace(cantonId) ? null : cantonId.Trim();
        ParishId = string.IsNullOrWhiteSpace(parishId) ? null : parishId.Trim();
        Latitude = string.IsNullOrWhiteSpace(latitude) ? null : latitude.Trim();
        Longitude = string.IsNullOrWhiteSpace(longitude) ? null : longitude.Trim();
        OpeningDate = openingDate;
        InternalNotes = string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes.Trim();
        IsMainBranch = isMainBranch;
        SetUpdated(updatedBy);
    }

    public void SetMainBranchFlag(bool isMain, Guid updatedBy)
    {
        IsMainBranch = isMain;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La sucursal", "deshabilitarse");

        base.Disable(updatedBy);
    }
}

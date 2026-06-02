using ERP.Domain.Common;

namespace ERP.Domain.Branches.Entities;

/// <summary>Sucursal operativa del ERP. CompanyId poblado desde Fase P1-1; null en registros legacy pre-migración.</summary>
public sealed class Branch : MasterEntity, ICompanyOperationalEntity
{
    /// <summary>Empresa a la que pertenece esta sucursal. Null = sucursal creada antes de P1-1.</summary>
    public Guid? CompanyId       { get; private set; }
    public string  Name            { get; private set; } = null!;
    public string  Address         { get; private set; } = null!;
    public string? Code            { get; private set; }
    public string? BranchType      { get; private set; }
    public string? Reference       { get; private set; }
    public string? Phones          { get; private set; }
    public string? Email           { get; private set; }
    public string? ManagerName     { get; private set; }

    public string? CountryId  { get; private set; }
    public string? ProvinceId { get; private set; }
    public string? CantonId   { get; private set; }
    public string? ParishId   { get; private set; }

    public string?  Latitude        { get; private set; }
    public string?  Longitude       { get; private set; }
    public decimal? StorageCapacity { get; private set; }
    public decimal? DailySalesGoal  { get; private set; }
    /// <summary>Campo libre equivalente a <c>tienerecarga</c> del legado.</summary>
    public string? RechargeOption { get; private set; }

    public bool IsMainBranch { get; private set; }

    private Branch() { }

    public static Branch Create(
        Guid    subscriberId,
        string  name,
        string  address,
        string  code,
        string? branchType,
        string? reference,
        string? phones,
        string? email,
        string? managerName,
        string? countryId,
        string? provinceId,
        string? cantonId,
        string? parishId,
        string? latitude,
        string? longitude,
        decimal? storageCapacity,
        decimal? dailySalesGoal,
        string? rechargeOption,
        bool    isMainBranch,
        Guid    createdBy,
        Guid?   companyId = null)
    {
        var b = new Branch
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            CompanyId    = companyId,
            Name         = name.Trim(),
            Address      = address.Trim(),
            Code            = code,
            BranchType      = string.IsNullOrWhiteSpace(branchType) ? null : branchType.Trim(),
            Reference       = string.IsNullOrWhiteSpace(reference)   ? null : reference.Trim(),
            Phones          = string.IsNullOrWhiteSpace(phones)       ? null : phones.Trim(),
            Email           = string.IsNullOrWhiteSpace(email)        ? null : email.Trim(),
            ManagerName     = string.IsNullOrWhiteSpace(managerName)  ? null : managerName.Trim(),
            CountryId       = string.IsNullOrWhiteSpace(countryId)    ? null : countryId.Trim(),
            ProvinceId      = string.IsNullOrWhiteSpace(provinceId)   ? null : provinceId.Trim(),
            CantonId        = string.IsNullOrWhiteSpace(cantonId)     ? null : cantonId.Trim(),
            ParishId        = string.IsNullOrWhiteSpace(parishId)     ? null : parishId.Trim(),
            Latitude        = string.IsNullOrWhiteSpace(latitude)     ? null : latitude.Trim(),
            Longitude       = string.IsNullOrWhiteSpace(longitude)    ? null : longitude.Trim(),
            StorageCapacity = storageCapacity,
            DailySalesGoal  = dailySalesGoal,
            RechargeOption  = string.IsNullOrWhiteSpace(rechargeOption) ? null : rechargeOption.Trim(),
            IsMainBranch    = isMainBranch,
        };
        b.SetCreated(createdBy);
        return b;
    }

    public void Update(
        string   name,
        string   address,
        string?  branchType,
        string?  reference,
        string?  phones,
        string?  email,
        string?  managerName,
        string?  countryId,
        string?  provinceId,
        string?  cantonId,
        string?  parishId,
        string?  latitude,
        string?  longitude,
        decimal? storageCapacity,
        decimal? dailySalesGoal,
        string?  rechargeOption,
        bool     isMainBranch,
        Guid     updatedBy)
    {
        Name            = name.Trim();
        Address         = address.Trim();
        BranchType      = string.IsNullOrWhiteSpace(branchType)   ? null : branchType.Trim();
        Reference       = string.IsNullOrWhiteSpace(reference)    ? null : reference.Trim();
        Phones          = string.IsNullOrWhiteSpace(phones)        ? null : phones.Trim();
        Email           = string.IsNullOrWhiteSpace(email)         ? null : email.Trim();
        ManagerName     = string.IsNullOrWhiteSpace(managerName)   ? null : managerName.Trim();
        CountryId       = string.IsNullOrWhiteSpace(countryId)    ? null : countryId.Trim();
        ProvinceId      = string.IsNullOrWhiteSpace(provinceId)   ? null : provinceId.Trim();
        CantonId        = string.IsNullOrWhiteSpace(cantonId)     ? null : cantonId.Trim();
        ParishId        = string.IsNullOrWhiteSpace(parishId)     ? null : parishId.Trim();
        Latitude        = string.IsNullOrWhiteSpace(latitude)     ? null : latitude.Trim();
        Longitude       = string.IsNullOrWhiteSpace(longitude)    ? null : longitude.Trim();
        StorageCapacity = storageCapacity;
        DailySalesGoal  = dailySalesGoal;
        RechargeOption  = string.IsNullOrWhiteSpace(rechargeOption) ? null : rechargeOption.Trim();
        IsMainBranch    = isMainBranch;
        SetUpdated(updatedBy);
    }

    public void SetMainBranchFlag(bool isMain, Guid updatedBy)
    {
        IsMainBranch = isMain;
        SetUpdated(updatedBy);
    }

}

namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo MasterData — socios de negocio (business partners) y su
/// configuración por empresa.
/// </summary>
public static class MasterDataPermissions
{
    public const string BusinessPartnersView = "masterdata.businesspartners.view";
    public const string BusinessPartnersCreate = "masterdata.businesspartners.create";
    public const string BusinessPartnersUpdate = "masterdata.businesspartners.update";
    public const string BusinessPartnersDisable = "masterdata.businesspartners.disable";
    public const string BusinessPartnersConfigureCompany = "masterdata.businesspartners.configure-company";

    public const string PaymentTermsView    = "masterdata.payment-terms.view";
    public const string PaymentTermsManage  = "masterdata.payment-terms.manage";

    public static IReadOnlyList<string> BusinessPartnersAll { get; } =
    [
        BusinessPartnersView,
        BusinessPartnersCreate,
        BusinessPartnersUpdate,
        BusinessPartnersDisable,
        BusinessPartnersConfigureCompany,
    ];

    public static IReadOnlyList<string> PaymentTermsAll { get; } =
    [
        PaymentTermsView,
        PaymentTermsManage,
    ];
}

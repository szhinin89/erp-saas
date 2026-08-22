using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("masterdata", Icon = "👥", SortOrder = 10)]
public static class MasterDataModule
{
    // ── Contenedor: Clientes ───────────────────────────────────────
    [NavItem(
        "Clientes",
        LabelKey = "app.nav.item.masterdata.customers",
        SortOrder = 10,
        Id = "e1000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = MasterDataPermissions.BusinessPartnersView
            + ","
            + FinancePermissions.View
            + ","
            + MasterDataPermissions.PaymentTermsView
            + ","
            + PricingPermissions.View
    )]
    public const string CustomersGroup = "/masterdata/customers-group";

    [NavItem(
        "Customers",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.customers",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000101",
        ParentId = "e1000000-0000-4000-9000-000000000001"
    )]
    public const string Customers = "/masterdata/customers";

    [NavItem(
        "Condiciones de Crédito",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.creditTerms",
        SortOrder = 20,
        Id = "b2000000-0000-4000-9000-000000000001",
        ParentId = "e1000000-0000-4000-9000-000000000001"
    )]
    public const string CreditTerms = "/finance/credit-terms";

    [NavItem(
        "Payment Terms",
        Permission = MasterDataPermissions.PaymentTermsView,
        LabelKey = "app.nav.item.masterdata.paymentTerms",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000103",
        ParentId = "e1000000-0000-4000-9000-000000000001"
    )]
    public const string PaymentTermsCustomer = "/master/payment-terms";

    [NavItem(
        "Listas de Precios",
        Permission = PricingPermissions.View,
        LabelKey = "app.nav.item.pricing.priceLists",
        SortOrder = 40,
        Id = "b1000000-0000-4000-9000-000000000001",
        ParentId = "e1000000-0000-4000-9000-000000000001"
    )]
    public const string PriceLists = "/pricing";

    // ── Contenedor: Proveedores ────────────────────────────────────
    [NavItem(
        "Proveedores",
        LabelKey = "app.nav.item.masterdata.suppliers",
        SortOrder = 20,
        Id = "e1000000-0000-4000-9000-000000000002",
        PermissionsAnyCsv = MasterDataPermissions.BusinessPartnersView
    )]
    public const string SuppliersGroup = "/masterdata/suppliers-group";

    [NavItem(
        "Suppliers",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.suppliers",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000102",
        ParentId = "e1000000-0000-4000-9000-000000000002"
    )]
    public const string Suppliers = "/masterdata/suppliers";

    // Transportistas (MENU-P0-FIX-01): NavItem retirado a propósito — no existe backend
    // controller para api/v1/logistics/carriers (carrierService.ts en frontend apunta a un
    // endpoint inexistente, 404 en toda operación). Reintroducir el NavItem (ver historial de
    // este archivo para la definición previa) solo cuando exista el controller real.
}

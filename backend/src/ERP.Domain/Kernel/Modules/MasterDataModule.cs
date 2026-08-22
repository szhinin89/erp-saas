using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: aplanado — antes agrupaba Clientes/Proveedores en contenedores
// separados dentro del mismo módulo; ahora son ítems planos bajo "Clientes y proveedores"
// (el módulo completo ya representa ese dominio, un contenedor interno era redundante).
[Module("masterdata", Icon = "👥", SortOrder = 10)]
public static class MasterDataModule
{
    [NavItem(
        "Clientes",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.customers",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000101"
    )]
    public const string Customers = "/masterdata/customers";

    [NavItem(
        "Proveedores",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.suppliers",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000102"
    )]
    public const string Suppliers = "/masterdata/suppliers";

    [NavItem(
        "Condiciones de Pago",
        Permission = MasterDataPermissions.PaymentTermsView,
        LabelKey = "app.nav.item.masterdata.paymentTerms",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000103"
    )]
    public const string PaymentTermsCustomer = "/master/payment-terms";

    [NavItem(
        "Condiciones de Crédito",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.creditTerms",
        SortOrder = 40,
        Id = "b2000000-0000-4000-9000-000000000001"
    )]
    public const string CreditTerms = "/finance/credit-terms";

    // No listado explícitamente en el modelo de negocio de MENU-MODULE-REORG-01 (que solo
    // menciona Clientes/Proveedores/Condiciones de pago/crédito) — se mantiene visible para no
    // perder acceso a la pantalla real; no pertenece claramente a ningún otro módulo del
    // reorg (aplica a precios de venta a clientes por igual que a costos de proveedor).
    [NavItem(
        "Listas de Precios",
        Permission = PricingPermissions.View,
        LabelKey = "app.nav.item.pricing.priceLists",
        SortOrder = 50,
        Id = "b1000000-0000-4000-9000-000000000001"
    )]
    public const string PriceLists = "/pricing";

    // Transportistas (MENU-P0-FIX-01): NavItem retirado a propósito — no existe backend
    // controller para api/v1/logistics/carriers (carrierService.ts en frontend apunta a un
    // endpoint inexistente, 404 en toda operación). Reintroducir el NavItem (ver historial de
    // este archivo para la definición previa) solo cuando exista el controller real.
}

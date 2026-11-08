using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// PURCHASES-RECEPTION-SUPPLIER-RULE-01 (H4) — la NC con proveedor inactivo se permite solo porque
/// siempre se vincula a una compra existente; esa compra debe ser del mismo tenant y empresa. Las dos
/// resoluciones que usa el flujo (<c>GetBySupplierAndInvoiceNumberAsync</c> en el verificador de
/// Recepción, <c>GetByIdAsync</c> en <c>CreateDraftPurchaseCreditNoteHandler</c>) nunca devuelven la
/// compra de otra empresa del tenant ni de otro tenant (PostgreSQL real).
/// </summary>
public sealed partial class PurchaseInvoiceAffectedInvoiceResolutionTests
{
    [Fact]
    public async Task La_factura_afectada_de_otra_empresa_del_tenant_nunca_se_resuelve()
    {
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000010600";
        var invoice = BuildConfirmedInvoice(ctx, invoiceNumber);
        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        Guid otherCompanyId;
        await using (var db = CreateContext())
        {
            var other = Company.CreateManaged(
                ctx.TenantId, $"09{Random.Shared.Next(10000000, 99999999)}001", "Otra S.A.", createdBy: _userId);
            db.Companies.Add(other);
            await db.SaveChangesAsync();
            otherCompanyId = other.Id;
        }

        var repo = new PurchaseInvoiceRepository(
            CreateContext(ctx.TenantId, otherCompanyId),
            new FixedCurrentCompany(() => otherCompanyId)
        );

        (await repo.GetBySupplierAndInvoiceNumberAsync(ctx.TenantId, ctx.SupplierId, invoiceNumber))
            .Should().BeNull();
        (await repo.GetByIdAsync(ctx.TenantId, invoice.Id)).Should().BeNull();
    }

    [Fact]
    public async Task La_factura_afectada_de_otro_tenant_nunca_se_resuelve()
    {
        var owner = await SeedTenantAsync();
        var intruder = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000010601";
        var invoice = BuildConfirmedInvoice(owner, invoiceNumber);
        await using (var db = CreateContext(owner.TenantId, owner.CompanyId))
        {
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        var repo = new PurchaseInvoiceRepository(
            CreateContext(intruder.TenantId, intruder.CompanyId),
            new FixedCurrentCompany(() => intruder.CompanyId)
        );

        // El llamador solo conoce su propio tenant; ni con el id ni con el proveedor ajenos resuelve.
        (await repo.GetBySupplierAndInvoiceNumberAsync(intruder.TenantId, owner.SupplierId, invoiceNumber))
            .Should().BeNull();
        (await repo.GetByIdAsync(intruder.TenantId, invoice.Id)).Should().BeNull();
        (await repo.GetByIdAsync(owner.TenantId, invoice.Id))
            .Should().BeNull("el filtro global del contexto también exige el tenant del request");
    }
}

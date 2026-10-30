using System.Net;
using System.Net.Http.Json;
using ERP.Application.Common;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// READINESS-PREVENTIVE-1A/1B — Nota de Crédito 04 de una factura electrónica con dirección fiscal
/// vacía (PostgreSQL real, pipeline HTTP + DI reales, gate fiscal preventivo real). 1A fijó que la
/// devolución se comiteaba (Kardex, reembolso, secuencial 04) y el XML fallaba después; 1B invierte
/// esa expectativa. El fixture es compartido: dirección y SriSettings se revierten en finally.
/// </summary>
public sealed partial class SalesReturnEndToEndTests
{
    [Fact]
    public async Task NC04_de_factura_electronica_con_direccion_vacia_se_rechaza_sin_devolucion_reembolso_ni_secuencial()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        var sessionId = await OpenCashSessionAsync(openingAmount: 200m);
        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 1m, unitPrice: 10m);
        var draft = (
            await (await CreateDraftAsync(invoiceId, "Gate fiscal NC 04", (lineId, 1m)))
                .Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(JsonOptions)
        )!.Data!;

        string originalAddress;
        int? sequenceBefore;
        using (var scope = _f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            // El fixture solo tiene un punto de emisión físico (sin pipeline electrónico de factura):
            // el snapshot de la factura ya autorizada se marca electrónico, el estado que deja una
            // factura electrónica autorizada cuando la configuración fiscal se vacía después.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE sales_invoices SET emission_type = {(short)EmissionType.Electronic} WHERE id = {invoiceId}");
            var establishment = await db.Establishments.SingleAsync(e => e.CompanyId == _f.CompanyId && e.IsMain);
            originalAddress = establishment.Address;
            establishment.Update(establishment.Name, "", null, Guid.NewGuid());
            db.SriSettings.Add(SriSettings.Create(_f.TenantId, _f.CompanyId, environment: 1,
                emissionType: 1, wsdlUrl: "https://sri.invalid/never-called", Guid.NewGuid()));
            await db.SaveChangesAsync();
            sequenceBefore = await db.DocumentSequences.AsNoTracking()
                .Where(s => s.EmissionPointId == _f.EmissionPointId && s.DocTypeCode == "04")
                .Select(s => (int?)s.CurrentSeq).SingleOrDefaultAsync();
        }

        try
        {
            var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
            var body = await authorizeResponse.Content.ReadAsStringAsync();

            authorizeResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
            body.Should().Contain(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing)
                .And.Contain("[electronicEmission.matrixAddress]");

            using var verifyScope = _f.CreateDbScope();
            var verify = verifyScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var persisted = await verify.SalesReturns.AsNoTracking().SingleAsync(r => r.Id == draft.Id);
            persisted.Status.Should().Be(SalesReturnStatus.Draft);
            persisted.CreditNoteDocumentNumber.Should().BeNull();
            (await verify.DocumentSequences.AsNoTracking()
                    .Where(s => s.EmissionPointId == _f.EmissionPointId && s.DocTypeCode == "04")
                    .Select(s => (int?)s.CurrentSeq).SingleOrDefaultAsync())
                .Should().Be(sequenceBefore, "el secuencial 04 no se consume");
            (await verify.StockMovements.AsNoTracking()
                    .AnyAsync(m => m.SourceDocId == draft.Id && m.MovementType == StockMovementType.SaleReturn))
                .Should().BeFalse();
            (await verify.CashMovements.AsNoTracking()
                    .AnyAsync(m => m.ReferenceId == draft.Id && m.CashSessionId == sessionId))
                .Should().BeFalse("sin reembolso");
            (await verify.ElectronicDocuments.AsNoTracking()
                    .AnyAsync(d => d.SourceModule == "Sales" && d.SourceEntityId == draft.Id))
                .Should().BeFalse();
        }
        finally
        {
            using var scope = _f.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var establishment = await db.Establishments.SingleAsync(e => e.CompanyId == _f.CompanyId && e.IsMain);
            establishment.Update(establishment.Name, originalAddress, null, Guid.NewGuid());
            db.SriSettings.RemoveRange(db.SriSettings.Where(s => s.CompanyId == _f.CompanyId));
            await db.SaveChangesAsync();
        }
    }
}

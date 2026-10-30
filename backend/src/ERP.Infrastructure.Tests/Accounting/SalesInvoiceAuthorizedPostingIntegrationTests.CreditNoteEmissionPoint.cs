using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.Sales.Services;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// READINESS-PREVENTIVE-1B (verificación NC 04) — la NC reutiliza el punto de emisión de la factura
/// original (SalesReturn no tiene uno propio). Con ese punto inactivo, el gate preventivo y el
/// proveedor XML de la NC (que lo lee con IEmissionPointRepository.GetByIdAsync) aplican la misma
/// regla: el punto no es utilizable. Reactivarlo restablece la emisión. PostgreSQL real, contenedor
/// propio de esta clase de pruebas; solo la inspección del .p12 y el catálogo global son dobles.
/// </summary>
public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    [Fact]
    public async Task NC04_con_punto_de_emision_original_inactivo_el_gate_y_el_proveedor_XML_lo_rechazan_y_al_reactivarlo_se_permite()
    {
        Guid emissionPointId;
        await using (var seed = CreateContext())
        {
            var establishment = await seed.Establishments.SingleAsync(e => e.CompanyId == _companyId);
            establishment.Update(establishment.Name, "Av. Amazonas N24-03", null, _createdBy);
            seed.SriSettings.Add(EmissionRequirementsTestWiring.CompleteSriSettings(_tenantId, _companyId, _createdBy));
            var point = await seed.EmissionPoints.SingleAsync(e => e.CompanyId == _companyId);
            point.Disable(_createdBy);
            emissionPointId = point.Id;
            await seed.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var inactive = await EmissionRequirementsTestWiring.Real(db).ValidateAsync(
                new(_tenantId, _companyId, emissionPointId, SalesReturnCreditNoteDataProvider.CreditNoteDocTypeCode));

            inactive.CanIssue.Should().BeFalse();
            inactive.Issues.Should().ContainSingle().Which.Should().Be(
                new ElectronicEmissionRequirementIssue(ElectronicEmissionRequirementCodes.EmissionPoint,
                    ElectronicEmissionRequirementSeverity.Required,
                    ERP.Domain.Modules.Company.Interfaces.ReadinessActionTarget.EmissionPoints));
            // Misma lectura que usa SalesReturnCreditNoteDataProvider: sin gate, el XML de la NC
            // fallaría igual ("El punto de emisión de la factura original ya no existe o está inactivo").
            (await new EmissionPointRepository(db).GetByIdAsync(emissionPointId, _tenantId)).Should().BeNull();
        }

        await using (var reactivate = CreateContext())
        {
            (await reactivate.EmissionPoints.SingleAsync(e => e.Id == emissionPointId)).Enable(_createdBy);
            await reactivate.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        (await EmissionRequirementsTestWiring.Real(verify).ValidateAsync(
                new(_tenantId, _companyId, emissionPointId, SalesReturnCreditNoteDataProvider.CreditNoteDocTypeCode)))
            .CanIssue.Should().BeTrue();
    }
}

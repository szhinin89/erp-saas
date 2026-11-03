using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// SRI-TOGGLE-06 — pausa total de la emisión frente a ConsultaComprobante (verificación de anulaciones de
/// retención), contra PostgreSQL real con el interruptor REAL escrito por la ruta de producción. Con la
/// emisión pausada no hay consulta manual ni automática ni candidatos del polling, sin escrituras sobre la
/// solicitud; al rehabilitar, una presentación anterior solo se reanuda con una verificación explícita.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task SRI_TOGGLE_06_pausa_bloquea_ConsultaComprobante_manual_automatica_y_el_polling()
    {
        await SetEmissionAuditedAsync(true);
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;
        (await SubmitAnnulmentAsync(request.Id)).IsSuccess.Should().BeTrue();
        (await DueForVerificationAsync()).Should().Contain(request.Id, "habilitada y presentada después");

        await SetEmissionAuditedAsync(false);

        (await DueForVerificationAsync()).Should().NotContain(request.Id, "empresa pausada: sin polling");
        foreach (var userId in new[] { _userId, Guid.Empty })
        {
            var paused = await VerifyWithRealSwitchAsync(request.Id, userId);
            paused.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        }
        _sriStatus.Calls.Should().Be(0);
        var untouched = (await ReadAnnulmentAsync(retentionId))!;
        untouched.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);
        untouched.LastSriCheckAtUtc.Should().BeNull("la pausa no registra consultas");
        untouched.SriCheckCount.Should().Be(0);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);
    }

    [Fact]
    public async Task SRI_TOGGLE_06_al_rehabilitar_una_presentacion_anterior_requiere_verificacion_explicita()
    {
        await SetEmissionAuditedAsync(true);
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;
        (await SubmitAnnulmentAsync(request.Id)).IsSuccess.Should().BeTrue();
        await SetEmissionAuditedAsync(false);
        await SetEmissionAuditedAsync(true);

        (await DueForVerificationAsync()).Should().NotContain(request.Id, "presentada antes de la habilitación vigente");
        (await VerifyWithRealSwitchAsync(request.Id, Guid.Empty)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        _sriStatus.Calls.Should().Be(0);

        _sriStatus.FiscalStatus = SriFiscalStatus.PendingAnnulment;
        var explicitCheck = await VerifyWithRealSwitchAsync(request.Id, _userId);

        explicitCheck.IsSuccess.Should().BeTrue(explicitCheck.Error);
        _sriStatus.Calls.Should().Be(1);
        (await DueForVerificationAsync(DateTime.UtcNow.AddMinutes(1)))
            .Should().Contain(request.Id, "tras la acción explícita el polling continúa");
        (await VerifyWithRealSwitchAsync(request.Id, Guid.Empty)).IsSuccess.Should().BeTrue();
        _sriStatus.Calls.Should().Be(2);
    }

    private async Task<Result<RetentionAnnulmentRequest>> VerifyWithRealSwitchAsync(Guid requestId, Guid userId)
    {
        await using var db = CreateWiredContext();
        var company = new FixedCurrentCompany(_companyId);
        return await RetentionElectronicTestWiring
            .AnnulmentService(
                db,
                company,
                _sriStatus,
                new ElectronicEmissionSwitch(db),
                new PurchaseRetentionOriginCancellation(CancelHandler(db), new PurchaseInvoiceRepository(db, company))
            )
            .VerifyWithSriAsync(_tenantId, _companyId, requestId, userId)
            .ThenDispatchAsync(db);
    }

    private async Task<IReadOnlyList<Guid>> DueForVerificationAsync(DateTime? checkedBeforeUtc = null)
    {
        await using var db = CreateJobContext();
        return (await new RetentionAnnulmentRequestRepository(db)
                .GetDueForSriVerificationAsync(checkedBeforeUtc ?? DateTime.UtcNow, 50))
            .Select(x => x.Item3)
            .ToList();
    }
}

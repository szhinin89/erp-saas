using ERP.Application.Modules.Company.UseCases.GetDocumentSequences;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.DocumentSequences;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-UI-04 — <c>GetDocumentSequencesQueryHandler</c> con el repositorio
/// mockeado. Solo prueba el mapeo a DTO; el scoping real por tenant/empresa (query filters
/// globales de EF) se cubre indirectamente por el resto de la suite de <c>DocumentSequence</c>
/// contra Postgres real (mismo criterio que <c>ConfigureDocumentSequenceHandlerTests</c>).
/// </summary>
public sealed class GetDocumentSequencesQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private readonly Mock<IDocumentSequenceRepository> _sequenceRepo = new();

    private GetDocumentSequencesQueryHandler CreateHandler() => new(_sequenceRepo.Object);

    [Fact]
    public async Task Sin_secuencias_devuelve_lista_vacia()
    {
        _sequenceRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<DocumentSequence>)Array.Empty<DocumentSequence>());

        var result = await CreateHandler().Handle(new GetDocumentSequencesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Mapea_cada_secuencia_a_su_dto_con_el_estado_hasbeenused()
    {
        var configuredNotUsed = DocumentSequence.Create(TenantId, CompanyId, EmissionPointId, "01");
        configuredNotUsed.ConfigureNextNumber(850);

        var used = DocumentSequence.Create(TenantId, CompanyId, EmissionPointId, "07");
        used.CaptureAndIncrement();

        _sequenceRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<DocumentSequence>)new[] { configuredNotUsed, used });

        var result = await CreateHandler().Handle(new GetDocumentSequencesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var facturaDto = result.Value!.Single(d => d.DocTypeCode == "01");
        facturaDto.EmissionPointId.Should().Be(EmissionPointId);
        facturaDto.NextNumber.Should().Be(850);
        facturaDto.HasBeenUsed.Should().BeFalse();

        var retencionDto = result.Value!.Single(d => d.DocTypeCode == "07");
        retencionDto.HasBeenUsed.Should().BeTrue();
    }
}

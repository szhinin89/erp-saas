using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.DocumentSequences;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-03 — <c>ConfigureDocumentSequenceCommandHandler</c> con dependencias
/// mockeadas. La concurrencia real (advisory lock, transacción) y la persistencia de
/// <c>has_been_used</c> vía SQL raw en <c>CaptureNextAsync</c> se cubren aparte con Postgres real
/// en <c>ERP.Infrastructure.Tests.Persistence.DocumentSequenceConfigurationTests</c> — esta suite
/// solo prueba la orquestación del handler (acceso, catálogo, creación vs. reconfiguración, mapeo
/// de excepciones de dominio a <c>Result</c>).
/// </summary>
public sealed class ConfigureDocumentSequenceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private readonly Mock<IDocumentSequenceRepository> _sequenceRepo = new();
    private readonly Mock<IEmissionPointRepository> _emissionPointRepo = new();
    private readonly Mock<ISriDocTypeCatalogResolver> _docTypeResolver = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<ICurrentCompany> _currentCompany = new();

    public ConfigureDocumentSequenceHandlerTests()
    {
        _currentTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _currentCompany.SetupGet(x => x.CompanyId).Returns(CompanyId);
        _docTypeResolver
            .Setup(x => x.IsActiveElectronicDocTypeAsync("07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private ConfigureDocumentSequenceCommandHandler CreateHandler() =>
        new(
            _sequenceRepo.Object,
            _emissionPointRepo.Object,
            _docTypeResolver.Object,
            _currentTenant.Object,
            _currentCompany.Object
        );

    private static EmissionPoint CreateEmissionPoint() =>
        EmissionPoint.Create(
            TenantId,
            CompanyId,
            EstablishmentId,
            code: "001",
            name: "EP-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: Guid.NewGuid()
        );

    [Fact]
    public async Task Punto_de_emision_inexistente_o_de_otra_empresa_devuelve_NotFound()
    {
        // ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A — el handler ahora llama
        // GetByIdForCompanyAsync(tenantId, companyId, id): un punto de emisión de otra empresa del
        // mismo tenant nunca matchea ese predicado y el repo real devuelve null, exactamente como
        // uno inexistente — mismo resultado NotFound en ambos casos.
        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((EmissionPoint?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "07", 850),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        _sequenceRepo.Verify(
            r => r.AddAsync(It.IsAny<DocumentSequence>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Punto_de_emision_de_otra_empresa_nunca_se_resuelve_aunque_exista_con_ese_Id_en_el_tenant()
    {
        // Explícito: EmissionPointId SÍ existe en el tenant, pero pertenece a OtherCompanyId — el
        // handler pide GetByIdForCompanyAsync(TenantId, CompanyId, ...) con la empresa activa, así
        // que el repo (defensa en profundidad, filtra por CompanyId en el propio predicado) nunca
        // lo devuelve por esa vía, sin importar que exista bajo otro Id de empresa.
        var otherCompanyId = Guid.NewGuid();
        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((EmissionPoint?)null);
        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(
                    TenantId,
                    otherCompanyId,
                    EmissionPointId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateEmissionPoint());

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "07", 850),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task DocTypeCode_no_activo_en_catalogo_devuelve_ValidationFailure()
    {
        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateEmissionPoint());
        _docTypeResolver
            .Setup(x => x.IsActiveElectronicDocTypeAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "99", 850),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Secuencia_inexistente_se_crea_con_el_numero_configurado()
    {
        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateEmissionPoint());
        _sequenceRepo
            .Setup(r =>
                r.GetByEmissionPointAndDocTypeAsync(
                    EmissionPointId,
                    "07",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((DocumentSequence?)null);

        DocumentSequence? added = null;
        _sequenceRepo
            .Setup(r => r.AddAsync(It.IsAny<DocumentSequence>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentSequence, CancellationToken>((seq, _) => added = seq)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "07", 850),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextNumber.Should().Be(850);
        result.Value.HasBeenUsed.Should().BeFalse();
        added.Should().NotBeNull();
        added!.CurrentSeq.Should().Be(850);
        _sequenceRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Secuencia_existente_nunca_usada_se_reconfigura_sin_crear_una_nueva()
    {
        var existing = DocumentSequence.Create(TenantId, CompanyId, EmissionPointId, "07");

        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateEmissionPoint());
        _sequenceRepo
            .Setup(r =>
                r.GetByEmissionPointAndDocTypeAsync(
                    EmissionPointId,
                    "07",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "07", 900),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextNumber.Should().Be(900);
        existing.CurrentSeq.Should().Be(900);
        _sequenceRepo.Verify(
            r => r.AddAsync(It.IsAny<DocumentSequence>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "una secuencia ya existente se reconfigura in-place, nunca se duplica"
        );
    }

    [Fact]
    public async Task Secuencia_ya_usada_rechaza_el_ajuste_con_Conflict()
    {
        var existing = DocumentSequence.Create(TenantId, CompanyId, EmissionPointId, "07");
        existing.CaptureAndIncrement(); // simula al menos una captura real -> HasBeenUsed = true

        _emissionPointRepo
            .Setup(r =>
                r.GetByIdForCompanyAsync(TenantId, CompanyId, EmissionPointId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateEmissionPoint());
        _sequenceRepo
            .Setup(r =>
                r.GetByEmissionPointAndDocTypeAsync(
                    EmissionPointId,
                    "07",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ConfigureDocumentSequenceCommand(EmissionPointId, "07", 9000),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        _sequenceRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

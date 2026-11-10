using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.OrgConfig.UseCases.GetCompanyInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.OrgConfig;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01 — Configuración → Ventas: guardado atómico (OrgSettings +
/// PriceList.IsDefault en un único SaveChanges), permiso de Pricing, normalización "" → null,
/// validación contra catálogo y advertencias sobre valores antiguos inválidos.
/// </summary>
public sealed class CompanySalesPreferencesIntegrityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    internal static Mock<IUnitOfWork> TransactionalUnitOfWork()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        uow.Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<Func<CancellationToken, Task>, CancellationToken>((op, ct) => op(ct));
        return uow;
    }

    private static PriceList NewList(string code, bool isDefault) =>
        PriceList.Create(TenantId, CompanyId, code, code, "USD", isDefault, UserId);

    private sealed class HandlerFixture
    {
        public Mock<IOrgSettingsRepository> OrgRepo { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IConfigurationChangeLogger> ChangeLogger { get; } = new();
        public Mock<IRuntimePermissionAuthorizer> Authorizer { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = TransactionalUnitOfWork();
        public List<OrgSetting> Written { get; } = new();

        public HandlerFixture(bool canUpdatePricing = true)
        {
            OrgRepo
                .Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
                .Callback<OrgSetting, CancellationToken>((s, _) => Written.Add(s))
                .Returns(Task.CompletedTask);
            PriceLists
                .Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PriceList>());
            Authorizer
                .Setup(a =>
                    a.IsAuthorizedAsync(
                        PricingPermissions.Update,
                        UserId,
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(canUpdatePricing);
        }

        public void Holders(params PriceList[] lists) =>
            PriceLists
                .Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(lists);

        public void Exists(PriceList list) =>
            PriceLists
                .Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

        public UpsertCompanyInvoiceOrgSettingsCommandHandler Build()
        {
            var tenant = new Mock<ICurrentTenant>();
            tenant.SetupGet(t => t.TenantId).Returns(TenantId);
            var company = new Mock<ICurrentCompany>();
            company.SetupGet(c => c.CompanyId).Returns(CompanyId);
            var user = new Mock<ICurrentUser>();
            user.SetupGet(u => u.UserId).Returns(UserId);

            return new UpsertCompanyInvoiceOrgSettingsCommandHandler(
                OrgRepo.Object,
                PriceLists.Object,
                new PriceListDefaultAssigner(PriceLists.Object, ChangeLogger.Object, UnitOfWork.Object),
                Authorizer.Object,
                UnitOfWork.Object,
                Mock.Of<IInvoiceDefaultsCatalogValidator>(),
                tenant.Object,
                company.Object,
                user.Object
            );
        }

        public void VerifySingleTransaction() =>
            UnitOfWork.Verify(
                u =>
                    u.ExecuteInTransactionAsync(
                        It.IsAny<Func<CancellationToken, Task>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        public void VerifyNothingWritten()
        {
            Written.Should().BeEmpty();
            UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            OrgRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            PriceLists.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            ChangeLogger.Verify(
                l =>
                    l.LogAsync(
                        It.IsAny<ConfigurationChangeLogEntry>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        }
    }

    // ── Handler de escritura ────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_normaliza_vacios_a_null_y_devuelve_los_valores_persistidos()
    {
        var fx = new HandlerFixture();

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand("", "   ", null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultDocTypeCode.Should().BeNull();
        result.Value.DefaultSriPaymentMethodCode.Should().BeNull();
        fx.Written.Should().HaveCount(3).And.OnlyContain(s => s.Value == null);
        fx.VerifySingleTransaction();
    }

    [Fact]
    public async Task Upsert_sin_cambio_de_lista_no_exige_permiso_Pricing_ni_toca_PriceList()
    {
        var fx = new HandlerFixture(canUpdatePricing: false);
        var current = NewList("GENERAL", isDefault: true);
        fx.PriceLists
            .Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(" 01 ", "20", null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultDocTypeCode.Should().Be("01");
        result.Value.DefaultPriceListId.Should().Be(current.Id);
        fx.Authorizer.Verify(
            a =>
                a.IsAuthorizedAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        current.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Upsert_con_cambio_de_lista_sin_permiso_Pricing_rechaza_todo_sin_escribir()
    {
        var fx = new HandlerFixture(canUpdatePricing: false);
        var previous = NewList("GENERAL", isDefault: true);
        var target = NewList("MAYORISTA", isDefault: false);
        fx.Holders(previous);
        fx.Exists(target);

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(
                    "01",
                    "20",
                    null,
                    new CompanyDefaultPriceListChange(target.Id)
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        fx.UnitOfWork.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        previous.IsDefault.Should().BeTrue();
        target.IsDefault.Should().BeFalse();
        fx.VerifyNothingWritten();
    }

    [Fact]
    public async Task Upsert_con_lista_inexistente_o_de_otra_empresa_rechaza_todo_sin_escribir()
    {
        var fx = new HandlerFixture();
        var foreignId = Guid.NewGuid(); // el repo opera con el scope de la empresa activa: no la ve

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(
                    "01",
                    null,
                    null,
                    new CompanyDefaultPriceListChange(foreignId)
                ),
                CancellationToken.None
            );

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyNothingWritten();
    }

    [Fact]
    public async Task Upsert_con_lista_deshabilitada_rechaza_todo_sin_escribir()
    {
        var fx = new HandlerFixture();
        var target = NewList("MAYORISTA", isDefault: false);
        target.Disable(UserId);
        fx.Exists(target);

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(
                    "01",
                    null,
                    null,
                    new CompanyDefaultPriceListChange(target.Id)
                ),
                CancellationToken.None
            );

        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.VerifyNothingWritten();
    }

    [Fact]
    public async Task Upsert_reasigna_la_lista_y_guarda_todo_en_un_unico_SaveChanges()
    {
        var fx = new HandlerFixture();
        var previous = NewList("GENERAL", isDefault: true);
        var target = NewList("MAYORISTA", isDefault: false);
        fx.Holders(previous);
        fx.Exists(target);

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(
                    "01",
                    "20",
                    null,
                    new CompanyDefaultPriceListChange(target.Id)
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultPriceListId.Should().Be(target.Id);
        previous.IsDefault.Should().BeFalse();
        target.IsDefault.Should().BeTrue();
        fx.Written.Should().HaveCount(3);
        fx.VerifySingleTransaction();
        fx.PriceLists.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fx.OrgRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fx.ChangeLogger.Verify(
            l => l.LogAsync(It.IsAny<ConfigurationChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Upsert_con_PriceListId_null_quita_la_lista_predeterminada()
    {
        var fx = new HandlerFixture();
        var previous = NewList("GENERAL", isDefault: true);
        fx.Holders(previous);

        var result = await fx.Build()
            .Handle(
                new UpsertCompanyInvoiceOrgSettingsCommand(
                    null,
                    null,
                    null,
                    new CompanyDefaultPriceListChange(null)
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultPriceListId.Should().BeNull();
        previous.IsDefault.Should().BeFalse();
        fx.VerifySingleTransaction();
        fx.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.EntityId == previous.Id && e.OldValue == "true" && e.NewValue == "false"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── Validador del comando ───────────────────────────────────────────────

    private static (UpsertCompanyInvoiceOrgSettingsCommandValidator, Mock<IInvoiceDefaultsCatalogValidator>) BuildValidator()
    {
        var catalog = new Mock<IInvoiceDefaultsCatalogValidator>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        return (new UpsertCompanyInvoiceOrgSettingsCommandValidator(catalog.Object, tenant.Object), catalog);
    }

    [Fact]
    public async Task Validator_vacios_son_sin_configurar_y_no_consultan_catalogo()
    {
        var (validator, catalog) = BuildValidator();

        var result = await validator.ValidateAsync(
            new UpsertCompanyInvoiceOrgSettingsCommand("", " ", null)
        );

        result.IsValid.Should().BeTrue();
        catalog.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Validator_rechaza_valores_fuera_de_catalogo_con_el_campo_correcto()
    {
        var (validator, catalog) = BuildValidator();
        var termId = Guid.NewGuid();
        catalog
            .Setup(c => c.IsValidDocTypeCodeAsync("07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        catalog
            .Setup(c => c.IsValidSriPaymentMethodCodeAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        catalog
            .Setup(c => c.IsValidPaymentTermAsync(TenantId, termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await validator.ValidateAsync(
            new UpsertCompanyInvoiceOrgSettingsCommand("07", "99", termId)
        );

        result.IsValid.Should().BeFalse();
        result
            .Errors.Select(e => (e.PropertyName, e.ErrorMessage))
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("DefaultDocTypeCode", InvoiceDefaultsCatalogMessages.InvalidDocType),
                    (
                        "DefaultSriPaymentMethodCode",
                        InvoiceDefaultsCatalogMessages.InvalidSriPaymentMethod
                    ),
                    ("DefaultPaymentTermId", InvoiceDefaultsCatalogMessages.InvalidPaymentTerm),
                }
            );
    }

    // ── Regla única de catálogo ─────────────────────────────────────────────

    private static (
        InvoiceDefaultsCatalogValidator Validator,
        Mock<ISriDocTypeCatalogResolver> DocTypes,
        Mock<IPaymentTermRepository> Terms
    ) BuildCatalog()
    {
        var docTypes = new Mock<ISriDocTypeCatalogResolver>();
        var sri = new Mock<ISriCatalogLookupRepository>();
        var terms = new Mock<IPaymentTermRepository>();
        return (new InvoiceDefaultsCatalogValidator(docTypes.Object, sri.Object, terms.Object), docTypes, terms);
    }

    [Theory]
    [InlineData("01", true, true)]
    [InlineData("01", false, false)]
    [InlineData("07", true, false)]
    [InlineData("04", true, false)]
    public async Task Catalogo_solo_acepta_Factura_01_activa(string code, bool active, bool expected)
    {
        var (validator, docTypes, _) = BuildCatalog();
        docTypes
            .Setup(d => d.IsActiveDocTypeAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        (await validator.IsValidDocTypeCodeAsync(code)).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, new[] { "01" })]
    [InlineData(false, new string[0])]
    public async Task Catalogo_expone_los_tipos_permitidos_para_filtrar_el_selector(bool invoiceActive, string[] expected)
    {
        var (validator, docTypes, _) = BuildCatalog();
        docTypes
            .Setup(d => d.IsActiveDocTypeAsync("01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoiceActive);

        (await validator.GetAllowedDocTypeCodesAsync()).Should().Equal(expected);
    }

    [Fact]
    public async Task Catalogo_condicion_de_pago_exige_existente_activa_y_del_tenant()
    {
        var (validator, _, terms) = BuildCatalog();
        var active = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
        var inactive = PaymentTerm.Create(TenantId, "C30", "30 días", 1, 30, UserId);
        inactive.Disable(UserId);
        var foreignId = Guid.NewGuid(); // de otro tenant: el repo filtra por TenantId y no la ve
        terms
            .Setup(t => t.GetByIdAsync(TenantId, active.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);
        terms
            .Setup(t => t.GetByIdAsync(TenantId, inactive.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactive);

        (await validator.IsValidPaymentTermAsync(TenantId, active.Id)).Should().BeTrue();
        (await validator.IsValidPaymentTermAsync(TenantId, inactive.Id)).Should().BeFalse();
        (await validator.IsValidPaymentTermAsync(TenantId, foreignId)).Should().BeFalse();
    }

    // ── Lectura administrativa ──────────────────────────────────────────────

    [Fact]
    public async Task Get_conserva_valores_antiguos_invalidos_y_los_advierte_sin_borrarlos()
    {
        var orgRepo = new Mock<IOrgSettingsRepository>();
        var termId = Guid.NewGuid();
        OrgSetting Setting(string key, string? value, SettingDataType type) =>
            OrgSetting.Create(TenantId, CompanyId, OrgScope.Company, CompanyId, key, value, type, UserId);
        orgRepo
            .Setup(r =>
                r.GetAllForScopeAsync(
                    TenantId,
                    CompanyId,
                    OrgScope.Company,
                    CompanyId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[]
                {
                    Setting(OrgSettingKeys.Invoice.DefaultDocTypeCode, "07", SettingDataType.String),
                    Setting(OrgSettingKeys.Invoice.DefaultPaymentMethodCode, "", SettingDataType.String),
                    Setting(
                        OrgSettingKeys.Invoice.DefaultPaymentTermId,
                        termId.ToString(),
                        SettingDataType.Guid
                    ),
                }
            );
        var staleDefault = NewList("ANTIGUA", isDefault: true);
        staleDefault.Disable(UserId);
        var priceLists = new Mock<IPriceListRepository>();
        priceLists
            .Setup(r => r.GetDefaultFlagHoldersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { staleDefault });
        var catalog = new Mock<IInvoiceDefaultsCatalogValidator>();
        catalog
            .Setup(c => c.IsValidDocTypeCodeAsync("07", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        catalog
            .Setup(c => c.IsValidPaymentTermAsync(TenantId, termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(c => c.CompanyId).Returns(CompanyId);

        var result = await new GetCompanyInvoiceOrgSettingsQueryHandler(
            orgRepo.Object,
            priceLists.Object,
            catalog.Object,
            tenant.Object,
            company.Object
        ).Handle(new GetCompanyInvoiceOrgSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultDocTypeCode.Should().Be("07");
        result.Value.DefaultSriPaymentMethodCode.Should().BeNull();
        result.Value.DefaultPaymentTermId.Should().Be(termId);
        result.Value.DefaultPriceListId.Should().BeNull();
        result.Value.ConfigurationWarnings.Should().HaveCount(3);
        result.Value.ConfigurationWarnings.Should().Contain(InvoiceDefaultsCatalogMessages.InvalidDocType);
        result.Value.ConfigurationWarnings.Should().Contain(InvoiceDefaultsCatalogMessages.InvalidPaymentTerm);
        orgRepo.Verify(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Asignador_fuera_de_una_transaccion_falla_en_vez_de_guardar_parcialmente()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.HasActiveTransaction).Returns(false);
        var assigner = new PriceListDefaultAssigner(
            Mock.Of<IPriceListRepository>(),
            Mock.Of<IConfigurationChangeLogger>(),
            uow.Object
        );

        var act = () => assigner.AssignAsync(TenantId, Guid.NewGuid(), UserId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.Services;
using ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;
using ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;
using ERP.Application.Common.Persistence;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — regla única de modos, validador y reemplazo de conjunto
/// (sin persistencia real: la prueba PostgreSQL cubre unicidad, scope, concurrencia y auditoría).
/// </summary>
public sealed class SupplierExpenseSubcategoriesTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Supplier = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static SupplierExpenseSubcategory Assignment(Guid sub, bool active = true)
    {
        var a = SupplierExpenseSubcategory.Create(Tenant, Company, Supplier, sub, User);
        if (!active)
            a.Disable(User);
        return a;
    }

    private static ExpenseSubcategoryInfo Info(Guid id, bool active = true, bool isSub = true) =>
        new(id, "S-" + id.ToString("N")[..4], "Sub " + id.ToString("N")[..4], "Cat", "Tipo", active, isSub);

    // ── Modos ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cero_asignaciones_activas_es_Unconfigured_aunque_existan_filas_retiradas()
    {
        var s1 = Guid.NewGuid();
        var scope = SupplierExpenseSubcategoryScope.From([Assignment(s1, active: false)], [Info(s1)]);

        scope.Mode.Should().Be(SupplierExpenseSubcategoryMode.Unconfigured);
        scope.Allows(Guid.NewGuid()).Should().BeTrue();
        SupplierExpenseSubcategoryScope.From([], []).Mode.Should().Be(SupplierExpenseSubcategoryMode.Unconfigured);
    }

    [Fact]
    public void Asignaciones_activas_con_subcategoria_disponible_es_Restricted_y_solo_permite_las_disponibles()
    {
        var ok = Guid.NewGuid();
        var inactiveNode = Guid.NewGuid();
        var retired = Guid.NewGuid();
        var scope = SupplierExpenseSubcategoryScope.From(
            [Assignment(ok), Assignment(inactiveNode), Assignment(retired, active: false)],
            [Info(ok), Info(inactiveNode, active: false), Info(retired)]);

        scope.Mode.Should().Be(SupplierExpenseSubcategoryMode.Restricted);
        scope.AvailableIds.Should().BeEquivalentTo([ok]);
        scope.Allows(ok).Should().BeTrue();
        scope.Allows(inactiveNode).Should().BeFalse();
        scope.Allows(retired).Should().BeFalse("una asignación retirada no cuenta");
        scope.Allows(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Asignaciones_activas_sin_ninguna_disponible_es_RestrictedEmpty_y_no_permite_nada()
    {
        var inactiveNode = Guid.NewGuid();
        var notSub = Guid.NewGuid();
        var scope = SupplierExpenseSubcategoryScope.From(
            [Assignment(inactiveNode), Assignment(notSub)],
            [Info(inactiveNode, active: false), Info(notSub, isSub: false)]);

        scope.Mode.Should().Be(SupplierExpenseSubcategoryMode.RestrictedEmpty);
        scope.AvailableIds.Should().BeEmpty();
        scope.Allows(inactiveNode).Should().BeFalse();
        scope.Allows(Guid.NewGuid()).Should().BeFalse("RestrictedEmpty nunca abre el catálogo");
    }

    [Fact]
    public void Dto_expone_modo_items_y_disponibles_calculados_por_el_servidor()
    {
        var ok = Guid.NewGuid();
        var retired = Guid.NewGuid();
        var dto = SupplierExpenseSubcategoriesDto.Build(
            [Assignment(ok), Assignment(retired, active: false)], [Info(ok), Info(retired)]);

        dto.Mode.Should().Be("Restricted");
        dto.AvailableSubcategoryIds.Should().BeEquivalentTo([ok]);
        dto.Items.Should().HaveCount(2);
        dto.Items.Single(i => i.ExpenseSubcategoryId == retired).IsActive.Should().BeFalse();
    }

    // ── Validador ────────────────────────────────────────────────────────────

    private static readonly SetSupplierExpenseSubcategoriesValidator Validator = new();

    private static bool IsValid(Guid supplier, IReadOnlyList<Guid> ids) =>
        Validator.Validate(new SetSupplierExpenseSubcategoriesCommand(supplier, ids)).IsValid;

    [Fact]
    public void Validador_acepta_el_conjunto_vacio_y_rechaza_duplicados_vacios_y_exceso()
    {
        IsValid(Supplier, []).Should().BeTrue();
        IsValid(Supplier, [Guid.NewGuid()]).Should().BeTrue();

        var dup = Guid.NewGuid();
        IsValid(Supplier, [dup, dup]).Should().BeFalse();
        IsValid(Supplier, [Guid.Empty]).Should().BeFalse();
        IsValid(Guid.Empty, []).Should().BeFalse();
        IsValid(Supplier,
            Enumerable.Range(0, SetSupplierExpenseSubcategoriesValidator.MaxSubcategories + 1)
                .Select(_ => Guid.NewGuid()).ToList()).Should().BeFalse();
    }

    // ── GET: catálogo candidato solo para quien configura ────────────────────

    private static async Task<Result<SupplierExpenseSubcategoriesDto>> GetResultAsync(
        string[] granted, Mock<ISupplierExpenseSubcategoryRepository>? repoOut = null)
    {
        var repo = new Mock<ISupplierExpenseSubcategoryRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repo.Setup(r => r.GetSubcategoryInfosAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repo.Setup(r => r.GetActiveSubcategoryCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Info(Guid.NewGuid()),
                Info(Guid.NewGuid(), active: false),
                Info(Guid.NewGuid(), isSub: false),
            ]);
        var bp = BusinessPartner.Create(Tenant, "04", "1790016919001", null, "Prov S.A.", User);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(r => r.GetByIdAsync(Supplier, It.IsAny<CancellationToken>())).ReturnsAsync(bp);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(c => c.UserId).Returns(User);
        ctx.SetupGet(c => c.Role).Returns("Member");
        ctx.SetupGet(c => c.HasCompany).Returns(true);
        var auth = new Mock<IRuntimePermissionAuthorizer>();
        auth.Setup(a => a.IsAuthorizedAsync(It.IsAny<string>(), User, "Member", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Guid _, string _, CancellationToken _) => granted.Contains(key));

        var result = await new GetSupplierExpenseSubcategoriesHandler(repo.Object, partners.Object, ctx.Object, auth.Object)
            .Handle(new GetSupplierExpenseSubcategoriesQuery(Supplier), CancellationToken.None);
        lastRepo = repo;
        return result;
    }

    private static Mock<ISupplierExpenseSubcategoryRepository>? lastRepo;

    private static async Task<(SupplierExpenseSubcategoriesDto Dto, Mock<ISupplierExpenseSubcategoryRepository> Repo)>
        GetAsync(bool canConfigure)
    {
        var result = await GetResultAsync(canConfigure
            ? [MasterDataPermissions.BusinessPartnersView, MasterDataPermissions.BusinessPartnersConfigureCompany]
            : [MasterDataPermissions.BusinessPartnersView]);
        result.IsSuccess.Should().BeTrue(because: result.Error);
        return (result.Value!, lastRepo!);
    }

    [Theory]
    [InlineData(MasterDataPermissions.BusinessPartnersView)]
    [InlineData(ExpensePermissions.DocumentsCreate)]
    [InlineData(ExpensePermissions.DocumentsUpdate)]
    public async Task Get_lo_pueden_leer_la_ficha_del_proveedor_y_la_captura_de_gastos_pero_sin_catalogo(string permission)
    {
        var result = await GetResultAsync([permission]);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Catalog.Should().BeNull("el catálogo candidato es solo de configure-company");
        lastRepo!.Verify(r => r.GetActiveSubcategoryCatalogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ExpensePermissions.DocumentsView)]
    [InlineData(ExpensePermissions.DocumentsConfirm)]
    [InlineData(ExpensePermissions.DocumentsCancel)]
    [InlineData(ExpensePermissions.CatalogView)]
    [InlineData(MasterDataPermissions.BusinessPartnersCreate)]
    public async Task Get_sin_ningun_permiso_de_lectura_es_Forbidden_y_no_consulta_datos(string unrelated)
    {
        var result = await GetResultAsync([unrelated]);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("FORBIDDEN");
        lastRepo!.Verify(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        lastRepo.Verify(r => r.GetActiveSubcategoryCatalogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_el_permiso_de_gastos_no_otorga_el_catalogo_aunque_el_usuario_capture_gastos()
    {
        var result = await GetResultAsync([ExpensePermissions.DocumentsCreate, ExpensePermissions.DocumentsUpdate]);

        result.Value!.Catalog.Should().BeNull();
    }

    [Fact]
    public async Task Get_sin_configure_company_no_devuelve_catalogo_ni_lo_consulta()
    {
        var (dto, repo) = await GetAsync(canConfigure: false);

        dto.Catalog.Should().BeNull();
        dto.Mode.Should().Be("Unconfigured");
        repo.Verify(r => r.GetActiveSubcategoryCatalogAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_con_configure_company_devuelve_solo_subcategorias_activas_sin_cuentas()
    {
        var (dto, _) = await GetAsync(canConfigure: true);

        dto.Catalog.Should().ContainSingle("se descartan el nodo inactivo y el que no es Subcategoría");
        dto.Catalog![0].Should().BeEquivalentTo(new { CategoryName = "Cat", TypeName = "Tipo" },
            o => o.ExcludingMissingMembers());
    }

    // ── Handler (reemplazo de conjunto) ──────────────────────────────────────

    private sealed class Fixture
    {
        public Mock<ISupplierExpenseSubcategoryRepository> Repo { get; } = new();
        public List<SupplierExpenseSubcategory> Existing { get; } = [];
        public List<ExpenseSubcategoryInfo> Infos { get; } = [];
        public List<SupplierExpenseSubcategory> Added { get; } = [];
        public bool SupplierActive { get; set; } = true;
        public bool HasSupplierRole { get; set; } = true;

        public SetSupplierExpenseSubcategoriesHandler Handler()
        {
            Repo.Setup(r => r.GetByBusinessPartnerAsync(Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Existing.Concat(Added).ToList());
            Repo.Setup(r => r.GetSubcategoryInfosAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                    Infos.Where(i => ids.Contains(i.Id)).ToList());
            Repo.Setup(r => r.AddAsync(It.IsAny<SupplierExpenseSubcategory>(), It.IsAny<CancellationToken>()))
                .Callback((SupplierExpenseSubcategory a, CancellationToken _) => Added.Add(a))
                .Returns(Task.CompletedTask);

            var bp = BusinessPartner.Create(Tenant, "04", "1790016919001", null, "Prov S.A.", User);
            if (!SupplierActive)
                bp.Deactivate(User);
            var partners = new Mock<IBusinessPartnerRepository>();
            partners.Setup(r => r.GetByIdAsync(Supplier, It.IsAny<CancellationToken>())).ReturnsAsync(bp);
            var roles = new Mock<IBusinessPartnerRoleRepository>();
            roles.Setup(r => r.GetByTypeAsync(Supplier, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(HasSupplierRole ? BusinessPartnerRole.Create(Tenant, Supplier, RoleType.Supplier, User) : null);
            var ctx = new Mock<IOperationalContext>();
            ctx.SetupGet(c => c.TenantId).Returns(Tenant);
            ctx.SetupGet(c => c.CompanyId).Returns(Company);
            ctx.SetupGet(c => c.UserId).Returns(User);
            ctx.SetupGet(c => c.HasCompany).Returns(true);
            return new SetSupplierExpenseSubcategoriesHandler(
                Repo.Object, partners.Object, roles.Object, ctx.Object, Mock.Of<IDatabaseExceptionTranslator>());
        }
    }

    [Fact]
    public async Task Set_crea_reactiva_y_retira_solo_lo_que_cambia()
    {
        var keep = Guid.NewGuid();
        var retire = Guid.NewGuid();
        var reactivate = Guid.NewGuid();
        var create = Guid.NewGuid();
        var fx = new Fixture();
        fx.Existing.AddRange([Assignment(keep), Assignment(retire), Assignment(reactivate, active: false)]);
        fx.Infos.AddRange([Info(keep), Info(retire), Info(reactivate), Info(create)]);
        var keepRow = fx.Existing[0];
        keepRow.ClearDomainEvents();

        var result = await fx.Handler().Handle(
            new(Supplier, [keep, reactivate, create]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        keepRow.DomainEvents.Should().BeEmpty("sin cambio de estado no hay evento auditable");
        fx.Existing[1].IsActive.Should().BeFalse();
        fx.Existing[2].IsActive.Should().BeTrue("se reutiliza el registro retirado");
        fx.Added.Should().ContainSingle().Which.ExpenseSubcategoryId.Should().Be(create);
        fx.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_vacio_retira_todas_las_activas_y_deja_Unconfigured()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var fx = new Fixture();
        fx.Existing.AddRange([Assignment(a), Assignment(b)]);
        fx.Infos.AddRange([Info(a), Info(b)]);

        var result = await fx.Handler().Handle(new(Supplier, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Mode.Should().Be("Unconfigured");
        fx.Existing.Should().OnlyContain(x => !x.IsActive);
        fx.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Set_rechaza_proveedor_inactivo_sin_rol_nodo_inexistente_no_subcategoria_o_inactivo()
    {
        var node = Guid.NewGuid();
        var notSub = Guid.NewGuid();
        var inactive = Guid.NewGuid();

        var inactiveSupplier = new Fixture { SupplierActive = false };
        (await inactiveSupplier.Handler().Handle(new(Supplier, []), default)).Code
            .Should().Be(ApiResponseCodes.Common.ValidationError);

        var noRole = new Fixture { HasSupplierRole = false };
        (await noRole.Handler().Handle(new(Supplier, []), default)).Code
            .Should().Be(ApiResponseCodes.Common.ValidationError);

        var fx = new Fixture();
        fx.Infos.AddRange([Info(node), Info(notSub, isSub: false), Info(inactive, active: false)]);
        (await fx.Handler().Handle(new(Supplier, [Guid.NewGuid()]), default)).Code
            .Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.Handler().Handle(new(Supplier, [notSub]), default)).Code
            .Should().Be(ApiResponseCodes.Common.ValidationError);
        (await fx.Handler().Handle(new(Supplier, [inactive]), default)).Code
            .Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_permite_retirar_una_asignacion_cuya_subcategoria_ya_esta_inactiva()
    {
        var gone = Guid.NewGuid();
        var other = Guid.NewGuid();
        var fx = new Fixture();
        fx.Existing.AddRange([Assignment(gone), Assignment(other)]);
        fx.Infos.AddRange([Info(gone, active: false), Info(other)]);

        var result = await fx.Handler().Handle(new(Supplier, [other]), default);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        fx.Existing[0].IsActive.Should().BeFalse();
    }
}

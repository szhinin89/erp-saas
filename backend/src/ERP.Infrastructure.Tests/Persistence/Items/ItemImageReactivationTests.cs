using ERP.Application.Common;
using ERP.Application.Items.UseCases.ItemImages;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Items;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Items;

public sealed class ItemImageReactivationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private Guid _tenant;
    private Guid _company;
    private Guid _otherCompany;
    private Guid _type;
    private readonly Guid _actor = Guid.NewGuid();
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        // Disposable database only; no migrations and no ERP data.
        await db.Database.EnsureCreatedAsync();
        var tenant = Tenant.Create("Image test", $"img-{Guid.NewGuid():N}", _actor);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, "1790012345001", "Image company", createdBy: _actor);
        var other = Company.CreateManaged(_tenant, "1790012345002", "Other company", createdBy: _actor);
        _company = company.Id;
        _otherCompany = other.Id;
        var type = ItemTypeDefinition.Create(_tenant, "IMAGE", "Image test", 0, _actor);
        _type = type.Id;
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        db.ItemTypes.Add(type);
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ErpDbContext Context(Guid? company = null, Guid? tenant = null) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
        Mock.Of<ICurrentTenant>(x => x.TenantId == (tenant ?? _tenant)), Mock.Of<IPublisher>(),
        Mock.Of<ICurrentCompany>(x => x.CompanyId == (company ?? _company) && x.HasCompanyContext == true && x.IsAuthenticated == true));
    private async Task<(Guid Item, Guid Image)> Seed(bool empty = false)
    {
        await using var db = Context();
        var item = Item.Create(_tenant, $"IMG-{Guid.NewGuid():N}", "Image", "Image test", _type, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), _actor, companyId: _company);
        if (!empty) item.ReplaceImages([(Guid.NewGuid(), "preserve", true, true, 7)], _actor);
        db.Items.Add(item);
        if (!empty) item.Images.Single().Disable(_actor);
        await db.SaveChangesAsync();
        return (item.Id, empty ? Guid.Empty : item.Images.Single().Id);
    }
    private EnableItemImageCommandHandler Enable(ErpDbContext db) => new(new ItemRepository(db), new UnitOfWork(db),
        Mock.Of<ICurrentTenant>(x => x.TenantId == _tenant), Mock.Of<ICurrentUser>(x => x.UserId == _actor));
    private DisableItemImageCommandHandler Disable(ErpDbContext db) => new(new ItemRepository(db), new UnitOfWork(db),
        Mock.Of<ICurrentTenant>(x => x.TenantId == _tenant), Mock.Of<ICurrentUser>(x => x.UserId == _actor));
    private ReplaceItemImagesCommandHandler Replace(ErpDbContext db) => new(new ItemRepository(db), new UnitOfWork(db),
        Mock.Of<ICurrentTenant>(x => x.TenantId == _tenant), Mock.Of<ICurrentUser>(x => x.UserId == _actor),
        Catalog(), Mock.Of<IItemTypeRepository>(), Mock.Of<IBusinessPartnerRepository>());

    private static ISriCatalogResolver Catalog()
    {
        var mock = new Mock<ISriCatalogResolver>();
        mock.Setup(x => x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriUomInfo>());
        mock.Setup(x => x.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriVatInfo>());
        mock.Setup(x => x.ResolveIceRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriIceInfo>());
        return mock.Object;
    }

    [Fact]
    public async Task Mapping_failure_after_replace_save_rolls_back_rows_and_item_audit()
    {
        var ids = await Seed();
        await using var before = Context();
        var audit = (await before.Items.SingleAsync(x => x.Id == ids.Item)).UpdatedAt;
        await using var db = Context();
        var catalog = new Mock<ISriCatalogResolver>();
        catalog.Setup(x => x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Injected mapping failure"));
        var handler = new ReplaceItemImagesCommandHandler(new ItemRepository(db), new UnitOfWork(db),
            Mock.Of<ICurrentTenant>(x => x.TenantId == _tenant), Mock.Of<ICurrentUser>(x => x.UserId == _actor),
            catalog.Object, Mock.Of<IItemTypeRepository>(), Mock.Of<IBusinessPartnerRepository>());
        Func<Task> act = () => handler.Handle(new(ids.Item, [new(Guid.NewGuid(), "replacement")]), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected mapping failure");
        db.Database.CurrentTransaction.Should().BeNull();
        await using var check = Context();
        var image = await check.ItemImages.SingleAsync(x => x.ItemId == ids.Item);
        image.Id.Should().Be(ids.Image); image.IsActive.Should().BeFalse();
        (await check.Items.SingleAsync(x => x.Id == ids.Item)).UpdatedAt.Should().Be(audit);
    }

    [Fact]
    public async Task Simultaneous_enable_preserves_metadata_and_noop_audit()
    {
        var ids = await Seed();
        await using var a = Context();
        await using var b = Context();
        var results = await Task.WhenAll(Enable(a).Handle(new(ids.Item, ids.Image), default), Enable(b).Handle(new(ids.Item, ids.Image), default));
        results.Should().OnlyContain(r => r.IsSuccess);
        await using var verify = Context();
        var image = await verify.ItemImages.SingleAsync(x => x.Id == ids.Image);
        image.IsMain.Should().BeFalse(); image.IsActive.Should().BeTrue();
        image.AltText.Should().Be("preserve"); image.SortOrder.Should().Be(7); image.IsEcommerce.Should().BeTrue();
        var audit = image.UpdatedAt;
        var itemAudit = (await verify.Items.SingleAsync(x => x.Id == ids.Item)).UpdatedAt;
        var xmin = await verify.Database.SqlQueryRaw<string>("SELECT xmin::text AS \"Value\" FROM item_images WHERE id = {0}", ids.Image).SingleAsync();
        await using var noop = Context();
        (await Enable(noop).Handle(new(ids.Item, ids.Image), default)).IsSuccess.Should().BeTrue();
        await verify.Entry(image).ReloadAsync();
        image.UpdatedAt.Should().Be(audit);
        (await verify.Items.AsNoTracking().SingleAsync(x => x.Id == ids.Item)).UpdatedAt.Should().Be(itemAudit);
        (await verify.Database.SqlQueryRaw<string>("SELECT xmin::text AS \"Value\" FROM item_images WHERE id = {0}", ids.Image).SingleAsync()).Should().Be(xmin);
    }

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    [InlineData("replace")]
    public async Task All_handlers_wait_for_same_item_lock(string operation)
    {
        var ids = await Seed();
        await using var owner = Context();
        var uow = new UnitOfWork(owner);
        await uow.BeginTransactionAsync();
        var item = await new ItemRepository(owner).GetWithImagesForUpdateAsync(ids.Item, _tenant);
        await using var contender = Context();
        Task mutation = operation switch
        {
            "enable" => Enable(contender).Handle(new(ids.Item, ids.Image), default),
            "disable" => Disable(contender).Handle(new(ids.Item, ids.Image), default),
            _ => Replace(contender).Handle(new(ids.Item, []), default)
        };
        await using var observer = Context();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await observer.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock') AS \"Value\"")
            .SingleAsync(timeout.Token))
        {
            await Task.Delay(20, timeout.Token);
        }
        mutation.IsCompleted.Should().BeFalse();
        item!.Images.Single().Enable(_actor);
        await owner.SaveChangesAsync();
        await uow.CommitAsync();
        await mutation.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enable_against_disable_or_replace(bool replace)
    {
        var ids = await Seed();
        await using var a = Context(); await using var b = Context();
        Task other = replace ? Replace(b).Handle(new(ids.Item, []), default) : Disable(b).Handle(new(ids.Item, ids.Image), default);
        await Task.WhenAll(Enable(a).Handle(new(ids.Item, ids.Image), default), other);
        await using var check = Context();
        var images = await check.ItemImages.Where(x => x.ItemId == ids.Item).ToListAsync();
        if (replace) images.Should().BeEmpty();
        else images.Should().ContainSingle();
    }

    [Fact]
    public async Task Empty_collection_replacements_do_not_merge()
    {
        var ids = await Seed(true);
        await using var a = Context(); await using var b = Context();
        await Task.WhenAll(Replace(a).Handle(new(ids.Item, [new(Guid.NewGuid(), "A", true)]), default),
            Replace(b).Handle(new(ids.Item, [new(Guid.NewGuid(), "B", true)]), default));
        await using var check = Context();
        (await check.ItemImages.Where(x => x.ItemId == ids.Item).ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Different_products_progress_and_rollback_restores_state()
    {
        var first = await Seed(); var second = await Seed();
        await using var a = Context();
        var uow = new UnitOfWork(a); await uow.BeginTransactionAsync();
        var item = await new ItemRepository(a).GetWithImagesForUpdateAsync(first.Item, _tenant);
        var audit = item!.Images.Single().UpdatedAt;
        item.Images.Single().Enable(_actor); await a.SaveChangesAsync();
        await using var b = Context();
        (await Enable(b).Handle(new(second.Item, second.Image), default).WaitAsync(TimeSpan.FromSeconds(5))).IsSuccess.Should().BeTrue();
        await uow.RollbackAsync();
        await using var check = Context();
        var image = await check.ItemImages.SingleAsync(x => x.Id == first.Image);
        image.IsActive.Should().BeFalse(); image.IsMain.Should().BeTrue(); image.UpdatedAt.Should().Be(audit);
    }

    [Fact]
    public async Task Existing_main_is_untouched_when_old_main_is_enabled()
    {
        var ids = await Seed();
        Guid mainId;
        await using (var db = Context())
        {
            var main = ItemImage.Create(ids.Item, _tenant, Guid.NewGuid(), "current main", true, false, 0, _actor);
            mainId = main.Id;
            db.ItemImages.Add(main); await db.SaveChangesAsync();
        }
        await using (var db = Context())
            (await Enable(db).Handle(new(ids.Item, ids.Image), default)).IsSuccess.Should().BeTrue();
        await using var check = Context();
        var current = await check.ItemImages.SingleAsync(x => x.Id == mainId);
        current.IsMain.Should().BeTrue(); current.IsActive.Should().BeTrue(); current.UpdatedAt.Should().BeNull();
        (await check.ItemImages.SingleAsync(x => x.Id == ids.Image)).IsMain.Should().BeFalse();
    }

    [Fact]
    public async Task Scope_membership_and_stale_tracking_are_rejected()
    {
        var ids = await Seed(); var other = await Seed();
        await using (var db = Context(_otherCompany))
            (await Enable(db).Handle(new(ids.Item, ids.Image), default)).IsSuccess.Should().BeFalse();
        await using (var db = Context(tenant: Guid.NewGuid()))
            (await Enable(db).Handle(new(ids.Item, ids.Image), default)).IsSuccess.Should().BeFalse();
        await using (var db = Context())
            (await Enable(db).Handle(new(ids.Item, other.Image), default)).IsSuccess.Should().BeFalse();
        await using var stale = Context();
        await new ItemRepository(stale).GetByIdLightAsync(ids.Item, _tenant);
        Func<Task> action = () => Enable(stale).Handle(new(ids.Item, ids.Image), default);
        await action.Should().ThrowAsync<InvalidOperationException>();
        stale.Database.CurrentTransaction.Should().BeNull();
    }
}

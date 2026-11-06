using ERP.Domain.Modules.Items.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Items;

public sealed class ItemImageTests
{
    [Fact]
    public void Enable_preserves_identity_metadata_and_creation_but_clears_main()
    {
        var creator = Guid.NewGuid(); var actor = Guid.NewGuid(); var variant = Guid.NewGuid();
        var image = ItemImage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " text ", true, true, 9, creator, variant);
        var id = image.Id; var storage = image.StorageObjectId; var created = image.CreatedAt;
        image.Disable(creator);
        image.Enable(actor).Should().BeTrue();
        image.Id.Should().Be(id); image.StorageObjectId.Should().Be(storage); image.VariantId.Should().Be(variant);
        image.AltText.Should().Be("text"); image.SortOrder.Should().Be(9); image.IsEcommerce.Should().BeTrue();
        image.IsActive.Should().BeTrue(); image.IsMain.Should().BeFalse();
        image.CreatedAt.Should().Be(created); image.CreatedBy.Should().Be(creator); image.UpdatedBy.Should().Be(actor);
        var updated = image.UpdatedAt;
        image.Enable(Guid.NewGuid()).Should().BeFalse();
        image.UpdatedAt.Should().Be(updated); image.UpdatedBy.Should().Be(actor);
    }

    [Fact]
    public void Already_active_main_is_unchanged_and_other_main_is_untouched()
    {
        var actor = Guid.NewGuid(); var item = Guid.NewGuid(); var tenant = Guid.NewGuid();
        var main = ItemImage.Create(item, tenant, Guid.NewGuid(), null, true, false, 0, actor);
        main.Enable(Guid.NewGuid()).Should().BeFalse();
        main.IsMain.Should().BeTrue(); main.UpdatedAt.Should().BeNull();
        var secondary = ItemImage.Create(item, tenant, Guid.NewGuid(), null, true, false, 1, actor);
        secondary.Disable(actor); secondary.Enable(actor);
        main.IsMain.Should().BeTrue(); main.UpdatedAt.Should().BeNull(); secondary.IsMain.Should().BeFalse();
    }
}

using ERP.Domain.Navigation.Entities;
using ERP.Domain.Subscribers.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SubscriberCustomMenuConfiguration : IEntityTypeConfiguration<SubscriberCustomMenu>
{
    public void Configure(EntityTypeBuilder<SubscriberCustomMenu> builder)
    {
        builder.ToTable("subscriber_custom_menus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.MenuConfigJson).HasColumnName("menu_config").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(x => x.SubscriberId).IsUnique().HasDatabaseName("ux_subscriber_custom_menus_subscriber");
        builder.HasOne<Subscriber>()
            .WithMany()
            .HasForeignKey(x => x.SubscriberId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_subscriber_custom_menus_subscriber_id");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBPUniqueIdentificationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Partial unique index: one active record per (subscriber, identification_type, identification_number).
            // Inactive records are excluded so the same ID can be "recycled" after deactivation.
            // Note: EF Core owned-type builder cannot express this index directly;
            // it is managed manually here (as documented in BusinessPartnerConfiguration.cs).
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_mbp_subscriber_identification
                ON master_business_partners (subscriber_id, identification_type, identification_number)
                WHERE is_active = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_mbp_subscriber_identification;");
        }
    }
}

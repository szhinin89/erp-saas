using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacySuperAdminWireValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE identity_users
                SET platform_role = 'PlatformOperator'
                WHERE platform_role = 'SuperAdmin';

                UPDATE password_reset_tokens
                SET user_kind = 'PlatformOperator'
                WHERE user_kind = 'SuperAdmin';

                UPDATE refresh_tokens
                SET user_type = 'PlatformOperator'
                WHERE user_type = 'SuperAdmin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE identity_users
                SET platform_role = 'SuperAdmin'
                WHERE platform_role = 'PlatformOperator';

                UPDATE password_reset_tokens
                SET user_kind = 'SuperAdmin'
                WHERE user_kind = 'PlatformOperator';

                UPDATE refresh_tokens
                SET user_type = 'SuperAdmin'
                WHERE user_type = 'PlatformOperator';
                """);
        }
    }
}

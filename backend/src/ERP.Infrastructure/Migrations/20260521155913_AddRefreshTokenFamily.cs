using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "parent_token_id",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rotation_depth",
                table: "refresh_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.Sql(
                "UPDATE refresh_tokens SET family_id = id WHERE family_id = '00000000-0000-0000-0000-000000000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "parent_token_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "rotation_depth",
                table: "refresh_tokens");
        }
    }
}

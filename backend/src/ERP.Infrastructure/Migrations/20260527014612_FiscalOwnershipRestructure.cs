using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiscalOwnershipRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add branch_id column with default (will be backfilled)
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "establishment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Step 2: Backfill branch_id from branches.establishment_id (reverse the old FK)
            migrationBuilder.Sql(@"
UPDATE establishment e
SET branch_id = b.id
FROM branches b
WHERE b.establishment_id = e.id
  AND b.establishment_id IS NOT NULL;
");

            // Step 3: Add FK and index now that the column is populated
            migrationBuilder.CreateIndex(
                name: "ix_establishment_branch_id",
                table: "establishment",
                column: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_establishment_branches_branch_id",
                table: "establishment",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Step 4: Drop the now-reversed FK column from branches
            migrationBuilder.DropColumn(
                name: "establishment_id",
                table: "branches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_establishment_branches_branch_id",
                table: "establishment");

            migrationBuilder.DropIndex(
                name: "ix_establishment_branch_id",
                table: "establishment");

            // Restore branches.establishment_id and backfill from establishment.branch_id
            migrationBuilder.AddColumn<Guid>(
                name: "establishment_id",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE branches b
SET establishment_id = e.id
FROM establishment e
WHERE e.branch_id = b.id
  AND e.is_main = true;
");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "establishment");
        }
    }
}

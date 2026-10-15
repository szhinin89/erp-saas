using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ERP.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20261015000000_BackgroundJobProgress")]
public sealed class BackgroundJobProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE background_job_progress (
            stream text PRIMARY KEY,
            candidate_id uuid NOT NULL,
            candidate_created_at timestamptz NOT NULL
        );
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE background_job_progress;");
}

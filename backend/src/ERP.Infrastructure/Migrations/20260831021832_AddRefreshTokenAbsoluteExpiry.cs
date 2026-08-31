using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenAbsoluteExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columna nullable primero para poder backfillear filas existentes con SQL antes de
            // endurecer a NOT NULL.
            migrationBuilder.AddColumn<DateTime>(
                name: "absolute_expires_at",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            // Los refresh tokens emitidos antes de esta migración no conocían el concepto de
            // "ventana absoluta de sesión". Se tratan como ya vencidos (absolute_expires_at =
            // expires_at = created_at) en vez de heredar una ventana nueva completa: cualquier
            // sesión previa a este deploy debe volver a autenticarse. Los tokens ya revocados no
            // se ven afectados en la práctica (IsRevoked ya los invalida primero).
            migrationBuilder.Sql(
                "UPDATE refresh_tokens SET absolute_expires_at = created_at, expires_at = created_at;"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "absolute_expires_at",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "absolute_expires_at",
                table: "refresh_tokens");
        }
    }
}

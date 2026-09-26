using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 (DATE-02E) — StockTransfer.TransferDate y
    /// StockAdjustment.AdjustmentDate son fechas de negocio: timestamptz (medianoche UTC) → date.
    ///
    /// Seguridad de datos:
    /// <list type="bullet">
    /// <item>Guard previo: si alguna fila NO es exactamente medianoche UTC (contiene hora real), la
    /// migración aborta sin tocar nada — nunca se descarta información horaria en silencio.</item>
    /// <item>Conversión explícita <c>(col AT TIME ZONE 'UTC')::date</c>: el cast implícito
    /// timestamptz→date usa el TimeZone de la sesión PostgreSQL y podría correr el día.</item>
    /// <item>Reversible sin pérdida: Down reconstruye exactamente la medianoche UTC previa
    /// (<c>col::timestamp AT TIME ZONE 'UTC'</c>).</item>
    /// </list>
    /// </summary>
    public partial class TemporalContractInventoryBusinessDates02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    bad_transfers integer;
                    bad_adjustments integer;
                BEGIN
                    SELECT count(*) INTO bad_transfers FROM stock_transfers
                     WHERE (transfer_date AT TIME ZONE 'UTC')::time <> TIME '00:00';
                    SELECT count(*) INTO bad_adjustments FROM stock_adjustments
                     WHERE (adjustment_date AT TIME ZONE 'UTC')::time <> TIME '00:00';
                    IF bad_transfers > 0 OR bad_adjustments > 0 THEN
                        RAISE EXCEPTION 'ZH-TEMPORAL-CONTRACT-02: % stock_transfers / % stock_adjustments con hora distinta de medianoche UTC; revisar antes de migrar a date.',
                            bad_transfers, bad_adjustments;
                    END IF;
                END $$;
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE stock_transfers
                    ALTER COLUMN transfer_date TYPE date USING (transfer_date AT TIME ZONE 'UTC')::date;
                ALTER TABLE stock_adjustments
                    ALTER COLUMN adjustment_date TYPE date USING (adjustment_date AT TIME ZONE 'UTC')::date;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE stock_transfers
                    ALTER COLUMN transfer_date TYPE timestamp with time zone USING (transfer_date::timestamp AT TIME ZONE 'UTC');
                ALTER TABLE stock_adjustments
                    ALTER COLUMN adjustment_date TYPE timestamp with time zone USING (adjustment_date::timestamp AT TIME ZONE 'UTC');
                """
            );
        }
    }
}

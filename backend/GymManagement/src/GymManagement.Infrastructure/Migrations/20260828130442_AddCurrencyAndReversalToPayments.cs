using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndReversalToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStartDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodEndDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountReceived",
                table: "Payments",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "varchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Payments",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesPaymentId",
                table: "Payments",
                type: "int",
                nullable: true);

            // Every payment taken before this migration was USD at face value, with no
            // conversion involved. Backfill them rather than leaving Currency as the empty
            // string the AddColumn default puts there - an empty string does not parse back
            // to the Currency enum, so reading any older payment would throw.
            migrationBuilder.Sql(
                "UPDATE Payments SET Currency = 'Usd', AmountReceived = Amount WHERE Currency = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AmountReceived",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReversesPaymentId",
                table: "Payments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStartDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodEndDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);
        }
    }
}

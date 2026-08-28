using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialPaymentSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SettledByPaymentId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ClientId_PackageId_SettledByPaymentId",
                table: "Payments",
                columns: new[] { "ClientId", "PackageId", "SettledByPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SettledByPaymentId",
                table: "Payments",
                column: "SettledByPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_SettledByPaymentId",
                table: "Payments",
                column: "SettledByPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_SettledByPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ClientId_PackageId_SettledByPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SettledByPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SettledByPaymentId",
                table: "Payments");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces the stored membership status with a single stored flag for the one value a
    /// person sets by hand.
    ///
    /// The status column was only ever written when someone edited a member. Its own comment
    /// said the values were kept current "by the nightly job", and that job was never
    /// written - so a membership that ran out went on reading Active indefinitely, every
    /// count of active members was wrong, and the door scanner planned for the next phase
    /// would have waved expired members straight through. Everything except Suspended is now
    /// worked out from the dates whenever it is asked for, so there is no copy left to drift.
    ///
    /// The order below matters. The scaffolded version dropped the old column before adding
    /// the new one, which would have thrown away every freeze on the way past.
    /// </summary>
    public partial class DeriveMembershipStatusFromDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "Clients",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Carry the freezes across before the column they live in disappears. Every
            // other status is recomputed from the dates and needs nothing kept.
            migrationBuilder.Sql(@"
                UPDATE `Clients`
                SET `IsSuspended` = 1
                WHERE `MembershipStatus` = 'Suspended';
            ");

            migrationBuilder.DropIndex(
                name: "IX_Clients_MembershipStatus",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MembershipStatus",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsSuspended",
                table: "Clients",
                column: "IsSuspended");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MembershipStatus",
                table: "Clients",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Rebuild the column the old code expects, filled in with the status each member
            // has today. This is the same rule as Client.StatusFrom and ClientQueries,
            // written a third time because a rollback has to leave the previous version of
            // the app with data it can actually read - an empty status column would make
            // every member Pending and lock the gym out of its own memberships.
            //
            // The seven days is Client.ExpiringWindowDays. A migration is a fixed record of
            // one moment, so it cannot reference the constant; if that window ever changes,
            // this number stays as it was when the migration was written, which is correct.
            migrationBuilder.Sql(@"
                UPDATE `Clients`
                SET `MembershipStatus` = CASE
                    WHEN `IsSuspended` = 1 THEN 'Suspended'
                    WHEN `MembershipStartDate` IS NULL OR `MembershipEndDate` IS NULL THEN 'Pending'
                    WHEN DATE(`MembershipStartDate`) > CURDATE() THEN 'Pending'
                    WHEN DATE(`MembershipEndDate`) < CURDATE() THEN 'Expired'
                    WHEN DATEDIFF(DATE(`MembershipEndDate`), CURDATE()) <= 7 THEN 'Expiring'
                    ELSE 'Active'
                END;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsSuspended",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_MembershipStatus",
                table: "Clients",
                column: "MembershipStatus");
        }
    }
}

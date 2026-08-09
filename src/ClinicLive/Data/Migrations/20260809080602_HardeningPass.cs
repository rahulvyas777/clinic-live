using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicLive.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardeningPass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_appointments_starts_at",
                table: "appointments",
                newName: "ix_appointments_starts_at_all");

            migrationBuilder.AddCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments",
                sql: "status IN ('Booked', 'CheckedIn', 'InProgress', 'Done', 'Cancelled', 'NoShow')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_appointments_status",
                table: "appointments");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_starts_at_all",
                table: "appointments",
                newName: "ix_appointments_starts_at");
        }
    }
}

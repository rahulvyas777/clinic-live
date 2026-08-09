using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicLive.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitSlotIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_appointments_starts_at_all",
                table: "appointments",
                newName: "ix_appointments_slot_active_unique");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_starts_at_all",
                table: "appointments",
                column: "starts_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_starts_at_all",
                table: "appointments");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_slot_active_unique",
                table: "appointments",
                newName: "ix_appointments_starts_at_all");
        }
    }
}

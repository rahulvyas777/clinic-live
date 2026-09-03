using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClinicLive.Data.Migrations
{
    /// <inheritdoc />
    public partial class PocketDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_notified_at",
                table: "queue_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_registrations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    appointment_id = table.Column<long>(type: "bigint", nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_registrations", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_registrations_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_registrations_appointment_id",
                table: "device_registrations",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_registrations_token",
                table: "device_registrations",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_registrations");

            migrationBuilder.DropColumn(
                name: "next_notified_at",
                table: "queue_entries");
        }
    }
}

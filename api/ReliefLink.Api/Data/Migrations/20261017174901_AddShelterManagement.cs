using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliefLink.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShelterManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shelters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    District = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelters", x => x.Id);
                    table.CheckConstraint("CK_Shelters_Capacity", "\"Capacity\" BETWEEN 1 AND 100000");
                    table.CheckConstraint("CK_Shelters_Latitude", "\"Latitude\" BETWEEN 5.5 AND 10.0");
                    table.CheckConstraint("CK_Shelters_Longitude", "\"Longitude\" BETWEEN 79.4 AND 82.0");
                });

            migrationBuilder.CreateTable(
                name: "ShelterCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReliefRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    HouseholdSize = table.Column<int>(type: "integer", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelterCheckIns", x => x.Id);
                    table.CheckConstraint("CK_ShelterCheckIns_HouseholdSize", "\"HouseholdSize\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ShelterCheckIns_ReliefRequests_ReliefRequestId",
                        column: x => x.ReliefRequestId,
                        principalTable: "ReliefRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShelterCheckIns_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShelterCheckIns_Users_CheckedInByUserId",
                        column: x => x.CheckedInByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterCheckIns_CheckedInByUserId",
                table: "ShelterCheckIns",
                column: "CheckedInByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelterCheckIns_ReliefRequestId",
                table: "ShelterCheckIns",
                column: "ReliefRequestId",
                unique: true,
                filter: "\"ReliefRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShelterCheckIns_ShelterId_CheckedOutAt",
                table: "ShelterCheckIns",
                columns: new[] { "ShelterId", "CheckedOutAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_District",
                table: "Shelters",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_IsActive",
                table: "Shelters",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShelterCheckIns");

            migrationBuilder.DropTable(
                name: "Shelters");
        }
    }
}

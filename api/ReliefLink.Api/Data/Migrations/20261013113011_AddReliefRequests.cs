using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliefLink.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReliefRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReliefRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdSize = table.Column<int>(type: "integer", nullable: false),
                    ElderlyCount = table.Column<int>(type: "integer", nullable: false),
                    ChildrenCount = table.Column<int>(type: "integer", nullable: false),
                    HomeFlooded = table.Column<bool>(type: "boolean", nullable: false),
                    District = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AddressText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NeededBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriageScore = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UrgencyOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    UrgencyOverrideReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TriageReasonsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReliefRequests", x => x.Id);
                    table.CheckConstraint("CK_ReliefRequests_Counts", "\"ElderlyCount\" >= 0 AND \"ChildrenCount\" >= 0 AND \"ElderlyCount\" + \"ChildrenCount\" <= \"HouseholdSize\"");
                    table.CheckConstraint("CK_ReliefRequests_HouseholdSize", "\"HouseholdSize\" BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_ReliefRequests_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_ReliefRequests_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_ReliefRequests_TriageScore", "\"TriageScore\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ReliefRequests_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReliefRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReliefRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReliefRequestItems", x => x.Id);
                    table.CheckConstraint("CK_ReliefRequestItems_Quantity", "\"Quantity\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ReliefRequestItems_ReliefRequests_ReliefRequestId",
                        column: x => x.ReliefRequestId,
                        principalTable: "ReliefRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequestItems_ReliefRequestId_Category",
                table: "ReliefRequestItems",
                columns: new[] { "ReliefRequestId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequests_CreatedAt",
                table: "ReliefRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequests_District",
                table: "ReliefRequests",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequests_RequesterId",
                table: "ReliefRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequests_Status",
                table: "ReliefRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReliefRequests_Urgency",
                table: "ReliefRequests",
                column: "Urgency");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_ReliefRequests_ReliefRequestId",
                table: "AgentWorkflows",
                column: "ReliefRequestId",
                principalTable: "ReliefRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_ReliefRequests_ReliefRequestId",
                table: "AgentWorkflows");

            migrationBuilder.DropTable(
                name: "ReliefRequestItems");

            migrationBuilder.DropTable(
                name: "ReliefRequests");
        }
    }
}

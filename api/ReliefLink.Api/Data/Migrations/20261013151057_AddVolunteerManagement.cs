using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliefLink.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Volunteers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    District = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxHoursPerWeek = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Volunteers", x => x.Id);
                    table.CheckConstraint("CK_Volunteers_MaxHoursPerWeek", "\"MaxHoursPerWeek\" BETWEEN 1 AND 168");
                    table.ForeignKey(
                        name: "FK_Volunteers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReliefRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RoleOrTask = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerAssignments", x => x.Id);
                    table.CheckConstraint("CK_VolunteerAssignments_TimeOrder", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_ReliefRequests_ReliefRequestId",
                        column: x => x.ReliefRequestId,
                        principalTable: "ReliefRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    SpecificStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SpecificEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_VolunteerAvailabilities_DateOrder", "\"SpecificEndUtc\" IS NULL OR \"SpecificStartUtc\" IS NULL OR \"SpecificEndUtc\" > \"SpecificStartUtc\"");
                    table.CheckConstraint("CK_VolunteerAvailabilities_TimeOrder", "\"EndTime\" IS NULL OR \"StartTime\" IS NULL OR \"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_VolunteerAvailabilities_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertificateReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerSkills_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_CreatedAt",
                table: "VolunteerAssignments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_CreatedByUserId",
                table: "VolunteerAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_ReliefRequestId",
                table: "VolunteerAssignments",
                column: "ReliefRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_StartTime_EndTime",
                table: "VolunteerAssignments",
                columns: new[] { "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_Status",
                table: "VolunteerAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_VolunteerId",
                table: "VolunteerAssignments",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAvailabilities_VolunteerId_DayOfWeek",
                table: "VolunteerAvailabilities",
                columns: new[] { "VolunteerId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAvailabilities_VolunteerId_SpecificStartUtc_Specif~",
                table: "VolunteerAvailabilities",
                columns: new[] { "VolunteerId", "SpecificStartUtc", "SpecificEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_CreatedAt",
                table: "Volunteers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_District",
                table: "Volunteers",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_IsActive",
                table: "Volunteers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_UserId",
                table: "Volunteers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerSkills_VolunteerId_Skill",
                table: "VolunteerSkills",
                columns: new[] { "VolunteerId", "Skill" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolunteerAssignments");

            migrationBuilder.DropTable(
                name: "VolunteerAvailabilities");

            migrationBuilder.DropTable(
                name: "VolunteerSkills");

            migrationBuilder.DropTable(
                name: "Volunteers");
        }
    }
}

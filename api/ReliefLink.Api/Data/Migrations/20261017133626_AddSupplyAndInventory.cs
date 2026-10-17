using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliefLink.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Depots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AddressText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CapacitySquareMeters = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Depots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    ReorderThreshold = table.Column<int>(type: "integer", nullable: false),
                    StandardFamilyAllocation = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyItems", x => x.Id);
                    table.CheckConstraint("CK_SupplyItems_ReorderThreshold", "\"ReorderThreshold\" >= 0");
                    table.CheckConstraint("CK_SupplyItems_StandardAllocation", "\"StandardFamilyAllocation\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "StockLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    QuantityReserved = table.Column<int>(type: "integer", nullable: false),
                    LastRestockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StorageBin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLevels", x => x.Id);
                    table.CheckConstraint("CK_StockLevels_OnHand_GTE_Reserved", "\"QuantityOnHand\" >= \"QuantityReserved\"");
                    table.CheckConstraint("CK_StockLevels_OnHand_NonNegative", "\"QuantityOnHand\" >= 0");
                    table.CheckConstraint("CK_StockLevels_Reserved_NonNegative", "\"QuantityReserved\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockLevels_Depots_DepotId",
                        column: x => x.DepotId,
                        principalTable: "Depots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockLevels_SupplyItems_SupplyItemId",
                        column: x => x.SupplyItemId,
                        principalTable: "SupplyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DepotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReliefRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.CheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockReservations_Depots_DepotId",
                        column: x => x.DepotId,
                        principalTable: "Depots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_ReliefRequests_ReliefRequestId",
                        column: x => x.ReliefRequestId,
                        principalTable: "ReliefRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockReservations_SupplyItems_SupplyItemId",
                        column: x => x.SupplyItemId,
                        principalTable: "SupplyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_Users_ReservedByUserId",
                        column: x => x.ReservedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QuantityChanged = table.Column<int>(type: "integer", nullable: false),
                    QuantityBefore = table.Column<int>(type: "integer", nullable: false),
                    QuantityAfter = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Depots_DepotId",
                        column: x => x.DepotId,
                        principalTable: "Depots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTransactions_SupplyItems_SupplyItemId",
                        column: x => x.SupplyItemId,
                        principalTable: "SupplyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTransactions_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Depots_Code",
                table: "Depots",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Depots_District",
                table: "Depots",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Depots_IsActive",
                table: "Depots",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StockLevels_DepotId_SupplyItemId",
                table: "StockLevels",
                columns: new[] { "DepotId", "SupplyItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLevels_QuantityOnHand",
                table: "StockLevels",
                column: "QuantityOnHand");

            migrationBuilder.CreateIndex(
                name: "IX_StockLevels_SupplyItemId",
                table: "StockLevels",
                column: "SupplyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_DepotId",
                table: "StockReservations",
                column: "DepotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ExpiresAt",
                table: "StockReservations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ReliefRequestId",
                table: "StockReservations",
                column: "ReliefRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ReservationNumber",
                table: "StockReservations",
                column: "ReservationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ReservedByUserId",
                table: "StockReservations",
                column: "ReservedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_Status",
                table: "StockReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_SupplyItemId",
                table: "StockReservations",
                column: "SupplyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_CreatedAt",
                table: "StockTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_DepotId_SupplyItemId",
                table: "StockTransactions",
                columns: new[] { "DepotId", "SupplyItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_PerformedByUserId",
                table: "StockTransactions",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_SupplyItemId",
                table: "StockTransactions",
                column: "SupplyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyItems_Category",
                table: "SupplyItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyItems_Code",
                table: "SupplyItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyItems_IsActive",
                table: "SupplyItems",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockLevels");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "StockTransactions");

            migrationBuilder.DropTable(
                name: "Depots");

            migrationBuilder.DropTable(
                name: "SupplyItems");
        }
    }
}

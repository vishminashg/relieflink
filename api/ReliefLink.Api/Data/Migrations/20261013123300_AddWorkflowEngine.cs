using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliefLink.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_ReliefRequestId",
                table: "AgentWorkflows");

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ValidationResults",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "ToolCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "UX_AgentWorkflows_ActivePerRequest",
                table: "AgentWorkflows",
                column: "ReliefRequestId",
                unique: true,
                filter: "\"Status\" IN ('Submitted','Planning','Proposed','Validating','PendingCoordinatorApproval','Approved','RevisionRequested')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AgentWorkflows_ActivePerRequest",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ValidationResults");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "ToolCalls");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_ReliefRequestId",
                table: "AgentWorkflows",
                column: "ReliefRequestId");
        }
    }
}

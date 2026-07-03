using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class Contractsthing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractorUsername",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_EngineerUsername",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_WorkerUsername",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AgreedTotalAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ApprovalsJson",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ArrivalConfirmed",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInStatus",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClientPenaltyPaid",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClientTerminationRequested",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractorUsername",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DailyAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DailySalary",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DaysWorked",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EngineerUsername",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EscrowAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "FirstWorkingDay",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "HistoryJson",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InstallmentsJson",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsSimpleContract",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "NoShowProcessed",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PenaltyConditions",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PenaltySplitContractorPercent",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PenaltySplitEngineerPercent",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SplitDays",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SplitEnabled",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkLocation",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerPenaltyPaid",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerTerminationRequested",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerUsername",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkplaceAddress",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "Balance",
                table: "UserWallets",
                newName: "FrozenBalance");

            migrationBuilder.RenameColumn(
                name: "WorkerUserId",
                table: "Contracts",
                newName: "TerminationReason");

            migrationBuilder.RenameColumn(
                name: "PenaltyClauseAmount",
                table: "Contracts",
                newName: "PenaltyAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "FreeBalance",
                table: "UserWallets",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "Contracts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailedAddress",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Governorate",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceProviderUserId",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ShiftEndTime",
                table: "Contracts",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ShiftStartTime",
                table: "Contracts",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminatedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminatedBy",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Contracts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalDays",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    DayNumber = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderArrived = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivalTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisputeReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisputeReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractDays_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ServiceProviderUserId",
                table: "Contracts",
                column: "ServiceProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDays_ContractId_Date",
                table: "ContractDays",
                columns: new[] { "ContractId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDays_ContractId_DayNumber",
                table: "ContractDays",
                columns: new[] { "ContractId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractDays_IsProcessed",
                table: "ContractDays",
                column: "IsProcessed");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ServiceProviderUserId",
                table: "Contracts",
                column: "ServiceProviderUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ServiceProviderUserId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractDays");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ServiceProviderUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "FreeBalance",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DetailedAddress",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Governorate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "RestrictedTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ServiceProviderUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ShiftEndTime",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ShiftStartTime",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TerminatedBy",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TotalDays",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "FrozenBalance",
                table: "UserWallets",
                newName: "Balance");

            migrationBuilder.RenameColumn(
                name: "TerminationReason",
                table: "Contracts",
                newName: "WorkerUserId");

            migrationBuilder.RenameColumn(
                name: "PenaltyAmount",
                table: "Contracts",
                newName: "PenaltyClauseAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedTotalAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalsJson",
                table: "Contracts",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ArrivalConfirmed",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInDate",
                table: "Contracts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckInStatus",
                table: "Contracts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInTime",
                table: "Contracts",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClientPenaltyPaid",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ClientTerminationRequested",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContractorUsername",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DailyAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DailySalary",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DaysWorked",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EngineerUsername",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "EscrowAmount",
                table: "Contracts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstWorkingDay",
                table: "Contracts",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoryJson",
                table: "Contracts",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstallmentsJson",
                table: "Contracts",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSimpleContract",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowProcessed",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyConditions",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "PenaltySplitContractorPercent",
                table: "Contracts",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PenaltySplitEngineerPercent",
                table: "Contracts",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SplitDays",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SplitEnabled",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkLocation",
                table: "Contracts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "WorkerPenaltyPaid",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorkerTerminationRequested",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WorkerUsername",
                table: "Contracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceAddress",
                table: "Contracts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractorUsername",
                table: "Contracts",
                column: "ContractorUsername");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EngineerUsername",
                table: "Contracts",
                column: "EngineerUsername");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_WorkerUserId",
                table: "Contracts",
                column: "WorkerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_WorkerUsername",
                table: "Contracts",
                column: "WorkerUsername");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerUserId",
                table: "Contracts",
                column: "WorkerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

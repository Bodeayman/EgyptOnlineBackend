using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddSimpleContractAndWalletTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceWalletNumber",
                table: "WithdrawRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletNumber",
                table: "UserWallets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientPhoneNumber",
                table: "DepositRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletOwnerName",
                table: "DepositRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

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
                name: "ClientUserId",
                table: "Contracts",
                type: "text",
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "IsSimpleContract",
                table: "Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Contracts",
                type: "text",
                nullable: true);

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
                name: "WorkerUserId",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkplaceAddress",
                table: "Contracts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInTime",
                table: "AttendanceRecords",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClientUserId",
                table: "Contracts",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_WorkerUserId",
                table: "Contracts",
                column: "WorkerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ClientUserId",
                table: "Contracts",
                column: "ClientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerUserId",
                table: "Contracts",
                column: "WorkerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ClientUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ClientUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SourceWalletNumber",
                table: "WithdrawRequests");

            migrationBuilder.DropColumn(
                name: "WalletNumber",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "RecipientPhoneNumber",
                table: "DepositRequests");

            migrationBuilder.DropColumn(
                name: "WalletOwnerName",
                table: "DepositRequests");

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
                name: "ClientUserId",
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
                name: "IsSimpleContract",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerPenaltyPaid",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerTerminationRequested",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkplaceAddress",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "AttendanceRecords");
        }
    }
}

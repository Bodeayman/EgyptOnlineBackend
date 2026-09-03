using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchAmountToContractDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatchAmount",
                table: "ContractDays",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchAmount",
                table: "ContractDays");
        }
    }
}

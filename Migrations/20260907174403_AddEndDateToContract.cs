using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddEndDateToContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Contracts");
        }
    }
}

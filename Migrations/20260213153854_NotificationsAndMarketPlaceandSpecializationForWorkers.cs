using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsAndMarketPlaceandSpecializationForWorkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DerivedSpec",
                table: "ServicesProviders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketPlace",
                table: "ServicesProviders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DerivedSpec",
                table: "ServicesProviders");

            migrationBuilder.DropColumn(
                name: "MarketPlace",
                table: "ServicesProviders");
        }
    }
}

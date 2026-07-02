using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddedPolymorphismToSpecilzation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DerivedSpec",
                table: "ServicesProviders");

            migrationBuilder.AddColumn<string>(
                name: "DerivedSpec",
                table: "Workers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DerivedSpec",
                table: "Engineers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DerivedSpec",
                table: "Assistants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DerivedSpec",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "DerivedSpec",
                table: "Engineers");

            migrationBuilder.DropColumn(
                name: "DerivedSpec",
                table: "Assistants");

            migrationBuilder.AddColumn<string>(
                name: "DerivedSpec",
                table: "ServicesProviders",
                type: "text",
                nullable: true);
        }
    }
}

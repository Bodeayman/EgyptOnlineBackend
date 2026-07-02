using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddedScupltor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sculptors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ServicePricePerDay = table.Column<decimal>(type: "numeric", nullable: false),
                    WorkerType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sculptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sculptors_ServicesProviders_Id",
                        column: x => x.Id,
                        principalTable: "ServicesProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sculptors");
        }
    }
}

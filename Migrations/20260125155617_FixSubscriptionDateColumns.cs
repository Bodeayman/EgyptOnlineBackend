using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class FixSubscriptionDateColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Subscriptions",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Subscriptions",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Subscriptions",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Subscriptions",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Subscriptions",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp"
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Subscriptions",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp"
            );
        }
    }
}

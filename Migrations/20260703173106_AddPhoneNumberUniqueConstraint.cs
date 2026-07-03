using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumberUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Remove duplicate phone numbers by setting duplicates to NULL
            // Keep the first occurrence of each phone number, set others to NULL
            migrationBuilder.Sql(@"
                WITH duplicates AS (
                    SELECT ""Id"", ""PhoneNumber"", ROW_NUMBER() OVER (PARTITION BY ""PhoneNumber"" ORDER BY ""Id"") AS rn
                    FROM ""AspNetUsers""
                    WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" != ''
                )
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = NULL
                WHERE ""Id"" IN (SELECT ""Id"" FROM duplicates WHERE rn > 1);
            ");

            // Step 2: Create a unique index on PhoneNumber that allows NULL values
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                filter: "\"PhoneNumber\" IS NOT NULL AND \"PhoneNumber\" != ''",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");
        }
    }
}

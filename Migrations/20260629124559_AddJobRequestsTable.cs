using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRequestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientUserId = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkerType = table.Column<int>(type: "integer", nullable: true),
                    PayRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequests_AspNetUsers_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequestInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: false),
                    ServiceProviderUserId = table.Column<string>(type: "text", nullable: false),
                    IsInterested = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequestInterests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequestInterests_AspNetUsers_ServiceProviderUserId",
                        column: x => x.ServiceProviderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRequestInterests_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestInterests_JobRequestId_ServiceProviderUserId",
                table: "JobRequestInterests",
                columns: new[] { "JobRequestId", "ServiceProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestInterests_ServiceProviderUserId",
                table: "JobRequestInterests",
                column: "ServiceProviderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_ClientUserId",
                table: "JobRequests",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_Governorate",
                table: "JobRequests",
                column: "Governorate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRequestInterests");

            migrationBuilder.DropTable(
                name: "JobRequests");
        }
    }
}

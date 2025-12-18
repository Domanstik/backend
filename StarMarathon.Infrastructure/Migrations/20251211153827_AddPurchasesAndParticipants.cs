using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarMarathon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasesAndParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contest_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FileUrls = table.Column<string>(type: "text", nullable: true),
                    AnswersJson = table.Column<string>(type: "text", nullable: true),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contest_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contest_participants_contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contest_participants_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceAtPurchase = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchases_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchases_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contest_participants_ContestId",
                table: "contest_participants",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_contest_participants_UserId",
                table: "contest_participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_ProductId",
                table: "purchases",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_UserId",
                table: "purchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contest_participants");

            migrationBuilder.DropTable(
                name: "purchases");
        }
    }
}

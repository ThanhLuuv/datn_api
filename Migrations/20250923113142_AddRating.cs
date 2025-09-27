using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookStore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PriceChanges",
                table: "PriceChanges");

            migrationBuilder.RenameTable(
                name: "PriceChanges",
                newName: "price_change");

            migrationBuilder.RenameColumn(
                name: "Isbn",
                table: "price_change",
                newName: "isbn");

            migrationBuilder.RenameColumn(
                name: "OldPrice",
                table: "price_change",
                newName: "old_price");

            migrationBuilder.RenameColumn(
                name: "NewPrice",
                table: "price_change",
                newName: "new_price");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "price_change",
                newName: "employee_id");

            migrationBuilder.RenameColumn(
                name: "ChangedAt",
                table: "price_change",
                newName: "changed_at");

            migrationBuilder.AddPrimaryKey(
                name: "PK_price_change",
                table: "price_change",
                columns: new[] { "isbn", "changed_at" });

            migrationBuilder.CreateTable(
                name: "rating",
                columns: table => new
                {
                    rating_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    isbn = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    stars = table.Column<int>(type: "int", nullable: false),
                    comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating", x => x.rating_id);
                    table.ForeignKey(
                        name: "fk_rating_book",
                        column: x => x.isbn,
                        principalTable: "book",
                        principalColumn: "isbn",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rating_customer",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_rating_customer_id",
                table: "rating",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_isbn_customer_id",
                table: "rating",
                columns: new[] { "isbn", "customer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rating");

            migrationBuilder.DropPrimaryKey(
                name: "PK_price_change",
                table: "price_change");

            migrationBuilder.RenameTable(
                name: "price_change",
                newName: "PriceChanges");

            migrationBuilder.RenameColumn(
                name: "isbn",
                table: "PriceChanges",
                newName: "Isbn");

            migrationBuilder.RenameColumn(
                name: "old_price",
                table: "PriceChanges",
                newName: "OldPrice");

            migrationBuilder.RenameColumn(
                name: "new_price",
                table: "PriceChanges",
                newName: "NewPrice");

            migrationBuilder.RenameColumn(
                name: "employee_id",
                table: "PriceChanges",
                newName: "EmployeeId");

            migrationBuilder.RenameColumn(
                name: "changed_at",
                table: "PriceChanges",
                newName: "ChangedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PriceChanges",
                table: "PriceChanges",
                columns: new[] { "Isbn", "ChangedAt" });
        }
    }
}

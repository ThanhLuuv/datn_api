using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookStore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCartTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "book_chk_2",
                table: "book");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "book",
                newName: "average_price");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "return",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "processed_at",
                table: "return",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "processed_by",
                table: "return",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "return",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "isbn",
                table: "price_change",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cart",
                columns: table => new
                {
                    cart_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart", x => x.cart_id);
                    table.ForeignKey(
                        name: "FK_cart_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cart_item",
                columns: table => new
                {
                    cart_item_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cart_id = table.Column<long>(type: "bigint", nullable: false),
                    isbn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    added_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_item", x => x.cart_item_id);
                    table.CheckConstraint("cart_item_chk_1", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_cart_item_book_isbn",
                        column: x => x.isbn,
                        principalTable: "book",
                        principalColumn: "isbn",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cart_item_cart_cart_id",
                        column: x => x.cart_id,
                        principalTable: "cart",
                        principalColumn: "cart_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_return_processed_by",
                table: "return",
                column: "processed_by");

            migrationBuilder.CreateIndex(
                name: "IX_price_change_employee_id",
                table: "price_change",
                column: "employee_id");

            migrationBuilder.AddCheckConstraint(
                name: "book_chk_2",
                table: "book",
                sql: "average_price >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_cart_customer_id",
                table: "cart",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_item_cart_id",
                table: "cart_item",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_item_isbn",
                table: "cart_item",
                column: "isbn");

            migrationBuilder.AddForeignKey(
                name: "fk_price_change_book",
                table: "price_change",
                column: "isbn",
                principalTable: "book",
                principalColumn: "isbn",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_price_change_employee",
                table: "price_change",
                column: "employee_id",
                principalTable: "employee",
                principalColumn: "employee_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_return_employee_processed_by",
                table: "return",
                column: "processed_by",
                principalTable: "employee",
                principalColumn: "employee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_price_change_book",
                table: "price_change");

            migrationBuilder.DropForeignKey(
                name: "fk_price_change_employee",
                table: "price_change");

            migrationBuilder.DropForeignKey(
                name: "FK_return_employee_processed_by",
                table: "return");

            migrationBuilder.DropTable(
                name: "cart_item");

            migrationBuilder.DropTable(
                name: "cart");

            migrationBuilder.DropIndex(
                name: "IX_return_processed_by",
                table: "return");

            migrationBuilder.DropIndex(
                name: "IX_price_change_employee_id",
                table: "price_change");

            migrationBuilder.DropCheckConstraint(
                name: "book_chk_2",
                table: "book");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "return");

            migrationBuilder.DropColumn(
                name: "processed_at",
                table: "return");

            migrationBuilder.DropColumn(
                name: "processed_by",
                table: "return");

            migrationBuilder.DropColumn(
                name: "status",
                table: "return");

            migrationBuilder.RenameColumn(
                name: "average_price",
                table: "book",
                newName: "unit_price");

            migrationBuilder.AlterColumn<string>(
                name: "isbn",
                table: "price_change",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "book_chk_2",
                table: "book",
                sql: "unit_price >= 0");
        }
    }
}

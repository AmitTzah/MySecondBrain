using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTextActionChatMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatMode",
                table: "TextActions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000001",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000002",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000003",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000004",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000005",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000006",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000007",
                column: "ChatMode",
                value: "TextCompletion");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000008",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000009",
                column: "ChatMode",
                value: "Standard");

            migrationBuilder.UpdateData(
                table: "TextActions",
                keyColumn: "Id",
                keyValue: "a000000000000000000000000000010",
                column: "ChatMode",
                value: "Standard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatMode",
                table: "TextActions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatOrganizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorited",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ThinkingContent",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "ChatThreads",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockNonce",
                table: "ChatThreads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockSalt",
                table: "ChatThreads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsActiveBranch",
                table: "Messages",
                column: "IsActiveBranch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_IsActiveBranch",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsFavorited",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ThinkingContent",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "LockNonce",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "LockSalt",
                table: "ChatThreads");
        }
    }
}

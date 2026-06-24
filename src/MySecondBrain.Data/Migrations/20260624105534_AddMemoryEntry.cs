using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 10240, nullable: false),
                    SourceThreadId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryEntries_ChatThreads_SourceThreadId",
                        column: x => x.SourceThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_CreatedAt",
                table: "MemoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Key",
                table: "MemoryEntries",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_SourceThreadId",
                table: "MemoryEntries",
                column: "SourceThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEntries");
        }
    }
}

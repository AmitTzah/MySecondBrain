using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnrichUsageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CacheCreationTokens",
                table: "UsageRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CacheReadTokens",
                table: "UsageRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "UsageRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErrorStatusCode",
                table: "UsageRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "UsageRecords",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatencyMs",
                table: "UsageRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawJsonPath",
                table: "UsageRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                table: "UsageRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CacheCreationTokens",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "CacheReadTokens",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ErrorStatusCode",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "ErrorType",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "LatencyMs",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "RawJsonPath",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "UsageRecords");
        }
    }
}

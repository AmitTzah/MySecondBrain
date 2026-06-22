using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModelConfigurationPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PricingCacheHitPer1K",
                table: "ModelConfigurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricingCacheMissPer1K",
                table: "ModelConfigurations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingCacheHitPer1K",
                table: "ModelConfigurations");

            migrationBuilder.DropColumn(
                name: "PricingCacheMissPer1K",
                table: "ModelConfigurations");
        }
    }
}

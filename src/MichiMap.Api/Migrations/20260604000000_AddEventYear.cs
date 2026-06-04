using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MichiMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventYear",
                table: "NaturalEvents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventYear",
                table: "NaturalEvents");
        }
    }
}

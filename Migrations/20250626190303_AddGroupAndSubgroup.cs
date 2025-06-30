using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnivMate.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupAndSubgroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationGroup",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocationSubgroup",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationGroup",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "LocationSubgroup",
                table: "Reports");
        }
    }
}

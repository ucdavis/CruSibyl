using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CruSibyl.Core.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddImportanceToApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Importance",
                table: "Apps",
                type: "float",
                nullable: false,
                defaultValue: 0.5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Importance",
                table: "Apps");
        }
    }
}

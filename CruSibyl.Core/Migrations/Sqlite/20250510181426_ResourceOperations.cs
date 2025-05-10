using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CruSibyl.Core.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ResourceOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoleOperations_Resource_Operation_RoleId",
                table: "RoleOperations");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "RoleOperations");

            migrationBuilder.DropColumn(
                name: "Resource",
                table: "RoleOperations");

            migrationBuilder.AddColumn<int>(
                name: "OperationId",
                table: "RoleOperations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "RoleOperations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleOperations_OperationId",
                table: "RoleOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleOperations_ResourceId_OperationId_RoleId",
                table: "RoleOperations",
                columns: new[] { "ResourceId", "OperationId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operations_Name",
                table: "Operations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Name",
                table: "Resources",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleOperations_Operations_OperationId",
                table: "RoleOperations",
                column: "OperationId",
                principalTable: "Operations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleOperations_Resources_ResourceId",
                table: "RoleOperations",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoleOperations_Operations_OperationId",
                table: "RoleOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleOperations_Resources_ResourceId",
                table: "RoleOperations");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_RoleOperations_OperationId",
                table: "RoleOperations");

            migrationBuilder.DropIndex(
                name: "IX_RoleOperations_ResourceId_OperationId_RoleId",
                table: "RoleOperations");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "RoleOperations");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "RoleOperations");

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "RoleOperations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Resource",
                table: "RoleOperations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RoleOperations_Resource_Operation_RoleId",
                table: "RoleOperations",
                columns: new[] { "Resource", "Operation", "RoleId" },
                unique: true);
        }
    }
}

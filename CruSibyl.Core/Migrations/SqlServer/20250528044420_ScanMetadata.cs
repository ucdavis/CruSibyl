using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CruSibyl.Core.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ScanMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLatestMajor",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "IsLatestMinor",
                table: "PackageVersions");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastScannedAt",
                table: "Repos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanMessage",
                table: "Repos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScanNumber",
                table: "Repos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "Repos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Major",
                table: "PackageVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Minor",
                table: "PackageVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Patch",
                table: "PackageVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreRelease",
                table: "PackageVersions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastScannedAt",
                table: "Packages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanMessage",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScanNumber",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repos_LastScannedAt",
                table: "Repos",
                column: "LastScannedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Repos_Name",
                table: "Repos",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repos_ScanNumber",
                table: "Repos",
                column: "ScanNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Repos_ScanStatus",
                table: "Repos",
                column: "ScanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LastScannedAt",
                table: "Packages",
                column: "LastScannedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ScanNumber",
                table: "Packages",
                column: "ScanNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ScanStatus",
                table: "Packages",
                column: "ScanStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Repos_LastScannedAt",
                table: "Repos");

            migrationBuilder.DropIndex(
                name: "IX_Repos_Name",
                table: "Repos");

            migrationBuilder.DropIndex(
                name: "IX_Repos_ScanNumber",
                table: "Repos");

            migrationBuilder.DropIndex(
                name: "IX_Repos_ScanStatus",
                table: "Repos");

            migrationBuilder.DropIndex(
                name: "IX_Packages_LastScannedAt",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_ScanNumber",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_ScanStatus",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "LastScannedAt",
                table: "Repos");

            migrationBuilder.DropColumn(
                name: "ScanMessage",
                table: "Repos");

            migrationBuilder.DropColumn(
                name: "ScanNumber",
                table: "Repos");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "Repos");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "Minor",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "Patch",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "PreRelease",
                table: "PackageVersions");

            migrationBuilder.DropColumn(
                name: "LastScannedAt",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ScanMessage",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ScanNumber",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "Packages");

            migrationBuilder.AddColumn<bool>(
                name: "IsLatestMajor",
                table: "PackageVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLatestMinor",
                table: "PackageVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}

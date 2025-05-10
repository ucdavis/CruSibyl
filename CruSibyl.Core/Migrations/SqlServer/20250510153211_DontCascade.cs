using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CruSibyl.Core.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class DontCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dependencies_Manifests_ManifestId",
                table: "Dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Dependencies_PackageVersions_PackageVersionId",
                table: "Dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_PlatformVersions_PlatformVersionId",
                table: "Manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_Repos_RepoId",
                table: "Manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteMappings_Notes_NoteId",
                table: "NoteMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Platforms_PlatformId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_PackageVersions_Packages_PackageId",
                table: "PackageVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformVersions_Platforms_PlatformId",
                table: "PlatformVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_TagMappings_Tags_TagId",
                table: "TagMappings");

            migrationBuilder.AddForeignKey(
                name: "FK_Dependencies_Manifests_ManifestId",
                table: "Dependencies",
                column: "ManifestId",
                principalTable: "Manifests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dependencies_PackageVersions_PackageVersionId",
                table: "Dependencies",
                column: "PackageVersionId",
                principalTable: "PackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_PlatformVersions_PlatformVersionId",
                table: "Manifests",
                column: "PlatformVersionId",
                principalTable: "PlatformVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_Repos_RepoId",
                table: "Manifests",
                column: "RepoId",
                principalTable: "Repos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteMappings_Notes_NoteId",
                table: "NoteMappings",
                column: "NoteId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Platforms_PlatformId",
                table: "Packages",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageVersions_Packages_PackageId",
                table: "PackageVersions",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformVersions_Platforms_PlatformId",
                table: "PlatformVersions",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TagMappings_Tags_TagId",
                table: "TagMappings",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dependencies_Manifests_ManifestId",
                table: "Dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Dependencies_PackageVersions_PackageVersionId",
                table: "Dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_PlatformVersions_PlatformVersionId",
                table: "Manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_Repos_RepoId",
                table: "Manifests");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteMappings_Notes_NoteId",
                table: "NoteMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Platforms_PlatformId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_PackageVersions_Packages_PackageId",
                table: "PackageVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformVersions_Platforms_PlatformId",
                table: "PlatformVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_TagMappings_Tags_TagId",
                table: "TagMappings");

            migrationBuilder.AddForeignKey(
                name: "FK_Dependencies_Manifests_ManifestId",
                table: "Dependencies",
                column: "ManifestId",
                principalTable: "Manifests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Dependencies_PackageVersions_PackageVersionId",
                table: "Dependencies",
                column: "PackageVersionId",
                principalTable: "PackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_PlatformVersions_PlatformVersionId",
                table: "Manifests",
                column: "PlatformVersionId",
                principalTable: "PlatformVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_Repos_RepoId",
                table: "Manifests",
                column: "RepoId",
                principalTable: "Repos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteMappings_Notes_NoteId",
                table: "NoteMappings",
                column: "NoteId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Platforms_PlatformId",
                table: "Packages",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PackageVersions_Packages_PackageId",
                table: "PackageVersions",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformVersions_Platforms_PlatformId",
                table: "PlatformVersions",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TagMappings_Tags_TagId",
                table: "TagMappings",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

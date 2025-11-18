using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CruSibyl.Core.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class WebJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Apps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ResourceGroup = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    RepoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefaultHostName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RuntimeStack = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastHealthCheckAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apps_Repos_RepoId",
                        column: x => x.RepoId,
                        principalTable: "Repos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RunMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Schedule = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExtraInfoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRunStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastRunDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebJobs_Apps_AppId",
                        column: x => x.AppId,
                        principalTable: "Apps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventApps",
                columns: table => new
                {
                    AppsId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventApps", x => new { x.AppsId, x.EventId });
                    table.ForeignKey(
                        name: "FK_EventApps_Apps_AppsId",
                        column: x => x.AppsId,
                        principalTable: "Apps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventApps_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventRepos",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReposId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRepos", x => new { x.EventId, x.ReposId });
                    table.ForeignKey(
                        name: "FK_EventRepos_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventRepos_Repos_ReposId",
                        column: x => x.ReposId,
                        principalTable: "Repos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventWebJobs",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    WebJobsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventWebJobs", x => new { x.EventId, x.WebJobsId });
                    table.ForeignKey(
                        name: "FK_EventWebJobs_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventWebJobs_WebJobs_WebJobsId",
                        column: x => x.WebJobsId,
                        principalTable: "WebJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apps_CreatedAt",
                table: "Apps",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Apps_Name",
                table: "Apps",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apps_RepoId",
                table: "Apps",
                column: "RepoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apps_ResourceGroup",
                table: "Apps",
                column: "ResourceGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Apps_SubscriptionId",
                table: "Apps",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventApps_EventId",
                table: "EventApps",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRepos_ReposId",
                table: "EventRepos",
                column: "ReposId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventType",
                table: "Events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Severity",
                table: "Events",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Timestamp",
                table: "Events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_EventWebJobs_WebJobsId",
                table: "EventWebJobs",
                column: "WebJobsId");

            migrationBuilder.CreateIndex(
                name: "IX_WebJobs_AppId_Name",
                table: "WebJobs",
                columns: new[] { "AppId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebJobs_JobType",
                table: "WebJobs",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_WebJobs_LastRunAt",
                table: "WebJobs",
                column: "LastRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebJobs_Status",
                table: "WebJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventApps");

            migrationBuilder.DropTable(
                name: "EventRepos");

            migrationBuilder.DropTable(
                name: "EventWebJobs");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "WebJobs");

            migrationBuilder.DropTable(
                name: "Apps");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HwSync.Persistence.Sqlite.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acknowledged_states",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acknowledged_states", x => new { x.ClientId, x.FolderId });
                });

            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participant",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant", x => x.Id);
                    table.CheckConstraint("ck_participant_singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "acknowledged_files",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acknowledged_files", x => new { x.ClientId, x.FolderId, x.RelativePath });
                    table.ForeignKey(
                        name: "FK_acknowledged_files_acknowledged_states_ClientId_FolderId",
                        columns: x => new { x.ClientId, x.FolderId },
                        principalTable: "acknowledged_states",
                        principalColumns: new[] { "ClientId", "FolderId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deletion_events",
                columns: table => new
                {
                    Number = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    OriginParticipantId = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    DeletedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    PreviousSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PreviousModifiedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deletion_events", x => x.Number);
                    table.CheckConstraint("ck_deletion_size", "PreviousSize >= 0");
                    table.ForeignKey(
                        name: "FK_deletion_events_folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_snapshots",
                columns: table => new
                {
                    FolderId = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_snapshots", x => new { x.FolderId, x.RelativePath });
                    table.CheckConstraint("ck_snapshot_size", "Size >= 0");
                    table.ForeignKey(
                        name: "FK_file_snapshots_folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deletion_events_FolderId_Number",
                table: "deletion_events",
                columns: new[] { "FolderId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_RootPath",
                table: "folders",
                column: "RootPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participant_ParticipantId",
                table: "participant",
                column: "ParticipantId",
                unique: true);

            migrationBuilder.Sql("INSERT INTO participant (Id, ParticipantId) VALUES (1, lower(hex(randomblob(16))))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acknowledged_files");

            migrationBuilder.DropTable(
                name: "deletion_events");

            migrationBuilder.DropTable(
                name: "file_snapshots");

            migrationBuilder.DropTable(
                name: "participant");

            migrationBuilder.DropTable(
                name: "acknowledged_states");

            migrationBuilder.DropTable(
                name: "folders");
        }
    }
}

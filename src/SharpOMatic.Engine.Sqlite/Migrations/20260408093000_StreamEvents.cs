using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class StreamEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreamEvents",
                schema: "SharpOMatic",
                columns: table => new
                {
                    StreamEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MessageRole = table.Column<int>(type: "INTEGER", nullable: true),
                    TextDelta = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamEvents", x => x.StreamEventId);
                    table.ForeignKey(
                        name: "FK_StreamEvents_Runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "SharpOMatic",
                        principalTable: "Runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "ConversationId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_RunId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "RunId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_WorkflowId_Created",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "WorkflowId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamEvents",
                schema: "SharpOMatic");
        }
    }
}

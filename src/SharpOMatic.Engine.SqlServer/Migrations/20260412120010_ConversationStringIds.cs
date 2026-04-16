
#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ConversationStringIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationCheckpoints_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConversationCheckpoints",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Conversations",
                schema: "SharpOMatic",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Conversations",
                schema: "SharpOMatic",
                table: "Conversations",
                column: "ConversationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConversationCheckpoints",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "ConversationId", "SequenceNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationCheckpoints_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationCheckpoints_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConversationCheckpoints",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Conversations",
                schema: "SharpOMatic",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConversationId",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Conversations",
                schema: "SharpOMatic",
                table: "Conversations",
                column: "ConversationId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConversationCheckpoints",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_ConversationId_SequenceNumber",
                schema: "SharpOMatic",
                table: "StreamEvents",
                columns: new[] { "ConversationId", "SequenceNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Assets",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationCheckpoints_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "ConversationCheckpoints",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Runs_Conversations_ConversationId",
                schema: "SharpOMatic",
                table: "Runs",
                column: "ConversationId",
                principalSchema: "SharpOMatic",
                principalTable: "Conversations",
                principalColumn: "ConversationId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

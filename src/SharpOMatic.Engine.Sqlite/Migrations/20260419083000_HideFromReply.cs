#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260419083000_HideFromReply")]
    public partial class HideFromReply : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideFromReply",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideFromReply",
                schema: "SharpOMatic",
                table: "StreamEvents");
        }
    }
}

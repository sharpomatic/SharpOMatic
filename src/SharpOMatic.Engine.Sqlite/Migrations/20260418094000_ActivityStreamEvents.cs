
#nullable disable

namespace SharpOMatic.Engine.Sqlite.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260418094000_ActivityStreamEvents")]
    public partial class ActivityStreamEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Replace",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityType",
                schema: "SharpOMatic",
                table: "StreamEvents");

            migrationBuilder.DropColumn(
                name: "Replace",
                schema: "SharpOMatic",
                table: "StreamEvents");
        }
    }
}

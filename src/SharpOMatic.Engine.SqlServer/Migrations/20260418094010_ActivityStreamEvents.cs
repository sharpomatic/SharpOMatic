
#nullable disable

namespace SharpOMatic.Engine.SqlServer.Migrations
{
    [DbContext(typeof(SharpOMaticDbContext))]
    [Migration("20260418094010_ActivityStreamEvents")]
    public partial class ActivityStreamEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Replace",
                schema: "SharpOMatic",
                table: "StreamEvents",
                type: "bit",
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

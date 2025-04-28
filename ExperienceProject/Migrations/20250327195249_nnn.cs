using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Experience.Migrations
{
    public partial class nnn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DislikesCount",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikesCount",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DislikesCount",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "LikesCount",
                table: "Comments");
        }
    }
}

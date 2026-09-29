using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "members",
                type: "text",
                nullable: false,
                defaultValue: "member");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "members");
        }
    }
}

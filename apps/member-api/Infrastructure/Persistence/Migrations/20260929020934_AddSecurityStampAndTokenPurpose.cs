using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityStampAndTokenPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "purpose",
                table: "verification_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "members",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "purpose",
                table: "verification_tokens");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "members");
        }
    }
}

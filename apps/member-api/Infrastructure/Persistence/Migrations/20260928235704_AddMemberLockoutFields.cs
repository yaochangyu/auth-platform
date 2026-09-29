using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberLockoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "members",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_end_at",
                table: "members",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "members");

            migrationBuilder.DropColumn(
                name: "lockout_end_at",
                table: "members");
        }
    }
}

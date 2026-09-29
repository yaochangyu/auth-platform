using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberEmailVerifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_verified_at",
                table: "members",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_verified_at",
                table: "members");
        }
    }
}

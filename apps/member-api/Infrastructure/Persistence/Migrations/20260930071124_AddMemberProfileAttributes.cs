using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberProfileAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "birthday",
                table: "members",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "education",
                table: "members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                table: "members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "members",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "members");

            migrationBuilder.DropColumn(
                name: "birthday",
                table: "members");

            migrationBuilder.DropColumn(
                name: "education",
                table: "members");

            migrationBuilder.DropColumn(
                name: "job_title",
                table: "members");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "members");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeveloperApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "developer_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: false),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    homepage_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_developer_applications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_developer_applications_client_id",
                table: "developer_applications",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_developer_applications_owner_member_id",
                table: "developer_applications",
                column: "owner_member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "developer_applications");
        }
    }
}

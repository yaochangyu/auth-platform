using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemberApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectedAppsAndMemberGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connected_apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    identifier = table.Column<string>(type: "text", nullable: false),
                    logo_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connected_apps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_grants_connected_apps_connected_app_id",
                        column: x => x.connected_app_id,
                        principalTable: "connected_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_grants_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_connected_apps_identifier",
                table: "connected_apps",
                column: "identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_grants_connected_app_id",
                table: "member_grants",
                column: "connected_app_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_grants_member_id",
                table: "member_grants",
                column: "member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_grants");

            migrationBuilder.DropTable(
                name: "connected_apps");
        }
    }
}

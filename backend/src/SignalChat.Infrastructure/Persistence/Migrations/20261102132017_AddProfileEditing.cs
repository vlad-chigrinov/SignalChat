using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvatarVersion",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GoogleAvatarUrl",
                table: "users",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCustomAvatar",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasCustomDisplayName",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Until now the avatar always came from Google, so the Google address is the current one.
            migrationBuilder.Sql("UPDATE users SET \"GoogleAvatarUrl\" = \"AvatarUrl\";");

            migrationBuilder.CreateTable(
                name: "user_avatars",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_avatars", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_avatars_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_avatars");

            migrationBuilder.DropColumn(
                name: "AvatarVersion",
                table: "users");

            migrationBuilder.DropColumn(
                name: "GoogleAvatarUrl",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HasCustomAvatar",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HasCustomDisplayName",
                table: "users");
        }
    }
}

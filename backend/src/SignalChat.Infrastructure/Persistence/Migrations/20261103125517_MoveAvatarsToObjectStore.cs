using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveAvatarsToObjectStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The uploaded pictures cannot be copied to the object store from SQL: users who had one go back to
            // their Google picture and can upload again. AvatarVersion keeps counting, so no cached address returns.
            migrationBuilder.Sql(
                "UPDATE users SET \"HasCustomAvatar\" = FALSE, \"AvatarUrl\" = \"GoogleAvatarUrl\" WHERE \"HasCustomAvatar\";");

            migrationBuilder.DropTable(
                name: "user_avatars");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}

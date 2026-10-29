using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "direct_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLowId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserHighId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direct_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    DirectConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.CheckConstraint("CK_messages_exactly_one_conversation", "(\"RoomId\" IS NULL) <> (\"DirectConversationId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_messages_direct_conversations_DirectConversationId",
                        column: x => x.DirectConversationId,
                        principalTable: "direct_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_members",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_members", x => new { x.RoomId, x.UserId });
                    table.ForeignKey(
                        name: "FK_room_members_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_direct_conversations_UserHighId",
                table: "direct_conversations",
                column: "UserHighId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_conversations_UserLowId_UserHighId",
                table: "direct_conversations",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_DirectConversationId_CreatedAt_Id",
                table: "messages",
                columns: new[] { "DirectConversationId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_messages_RoomId_CreatedAt_Id",
                table: "messages",
                columns: new[] { "RoomId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_room_members_UserId",
                table: "room_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_NormalizedName",
                table: "rooms",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "room_members");

            migrationBuilder.DropTable(
                name: "direct_conversations");

            migrationBuilder.DropTable(
                name: "rooms");
        }
    }
}

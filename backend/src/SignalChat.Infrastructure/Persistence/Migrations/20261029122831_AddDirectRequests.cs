using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorId",
                table: "direct_conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Conversations that existed before the consent flow were chats: they become accepted, and their
            // initiator is the lower user id (the value EF's default cannot express).
            migrationBuilder.Sql(
                "UPDATE direct_conversations SET \"InitiatorId\" = \"UserLowId\";");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "direct_conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "direct_conversations",
                type: "text",
                nullable: false,
                defaultValue: "Accepted");

            migrationBuilder.CreateIndex(
                name: "IX_direct_conversations_InitiatorId_Status",
                table: "direct_conversations",
                columns: new[] { "InitiatorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_direct_conversations_InitiatorId_Status",
                table: "direct_conversations");

            migrationBuilder.DropColumn(
                name: "InitiatorId",
                table: "direct_conversations");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "direct_conversations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "direct_conversations");
        }
    }
}

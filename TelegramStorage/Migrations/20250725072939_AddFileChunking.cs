using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TelegramStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddFileChunking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChunked",
                table: "FileRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalChunks",
                table: "FileRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FileChunks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileRecordId = table.Column<int>(type: "integer", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    TelegramFileId = table.Column<string>(type: "text", nullable: false),
                    TelegramMessageId = table.Column<string>(type: "text", nullable: true),
                    ChunkSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileChunks_FileRecords_FileRecordId",
                        column: x => x.FileRecordId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileChunks_FileRecordId_ChunkIndex",
                table: "FileChunks",
                columns: new[] { "FileRecordId", "ChunkIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileChunks_TelegramFileId",
                table: "FileChunks",
                column: "TelegramFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileChunks");

            migrationBuilder.DropColumn(
                name: "IsChunked",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "TotalChunks",
                table: "FileRecords");
        }
    }
}

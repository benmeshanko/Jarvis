using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jarvis.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Album = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    Plays = table.Column<int>(type: "INTEGER", nullable: false),
                    ListenTime = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => new { x.Artist, x.Album, x.Title });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Songs");
        }
    }
}

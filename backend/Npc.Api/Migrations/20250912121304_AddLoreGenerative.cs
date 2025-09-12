using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Npc.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoreGenerative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GeneratedAt",
                table: "lores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationMeta",
                table: "lores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSource",
                table: "lores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGenerated",
                table: "lores",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "lores");

            migrationBuilder.DropColumn(
                name: "GenerationMeta",
                table: "lores");

            migrationBuilder.DropColumn(
                name: "GenerationSource",
                table: "lores");

            migrationBuilder.DropColumn(
                name: "IsGenerated",
                table: "lores");
        }
    }
}

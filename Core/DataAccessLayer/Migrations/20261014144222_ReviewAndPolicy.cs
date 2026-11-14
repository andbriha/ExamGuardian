using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAndPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveClientId",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceClientId",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHistoryJson",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityNotes",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IntegrityReviewedAt",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrityReviewedBy",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityVerdict",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "UnderReview");

            migrationBuilder.AddColumn<int>(
                name: "PassingScoreAtStart",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSpeech",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedObjectsJson",
                table: "Exams",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "FlagHeadMovement",
                table: "Exams",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveClientId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "EvidenceClientId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IntegrityHistoryJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IntegrityNotes",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IntegrityReviewedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IntegrityReviewedBy",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IntegrityVerdict",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PassingScoreAtStart",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AllowSpeech",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "AllowedObjectsJson",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "FlagHeadMovement",
                table: "Exams");
        }
    }
}

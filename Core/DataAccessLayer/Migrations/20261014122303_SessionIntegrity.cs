using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class SessionIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Users_StudentId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Violations_ExamSessionId",
                table: "Violations");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_StudentId",
                table: "Sessions");

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Violations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ReviewDecision",
                table: "Violations",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ReviewHistoryJson",
                table: "Violations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "Violations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Violations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "Violations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Group\" text;");

            migrationBuilder.AddColumn<string>(
                name: "AdmissionId",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineUtc",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceStatus",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ExamSnapshotJson",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavedAnswersJson",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "InProgress");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedGroup",
                table: "Exams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PassingScore",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.Sql("UPDATE \"Violations\" SET \"ReviewDecision\" = 'LegacyReviewed' WHERE \"IsReviewed\";");
            migrationBuilder.Sql("UPDATE \"Sessions\" SET \"Status\" = 'LegacyCompleted', \"EvidenceStatus\" = 'Unknown' WHERE \"IsFinished\";");
            migrationBuilder.Sql("UPDATE \"Sessions\" s SET \"DeadlineUtc\" = s.\"StartTime\" + e.\"DurationMinutes\" * interval '1 minute' FROM \"Exams\" e WHERE e.\"Id\" = s.\"ExamId\";");
            migrationBuilder.CreateIndex(
                name: "IX_Violations_EventId",
                table: "Violations",
                column: "EventId",
                unique: true,
                filter: "\"EventId\" <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ExamSessionId_RelativeTimestampSeconds",
                table: "Violations",
                columns: new[] { "ExamSessionId", "RelativeTimestampSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StudentId_ExamId",
                table: "Sessions",
                columns: new[] { "StudentId", "ExamId" },
                unique: true,
                filter: "\"IsFinished\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Users_StudentId",
                table: "Sessions",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Users_StudentId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Violations_EventId",
                table: "Violations");

            migrationBuilder.DropIndex(
                name: "IX_Violations_ExamSessionId_RelativeTimestampSeconds",
                table: "Violations");

            migrationBuilder.DropIndex(
                name: "IX_Users_Login",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_StudentId_ExamId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ReviewHistoryJson",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AdmissionId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeadlineUtc",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "EvidenceStatus",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ExamSnapshotJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SavedAnswersJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AllowedGroup",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ExamSessionId",
                table: "Violations",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StudentId",
                table: "Sessions",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Users_StudentId",
                table: "Sessions",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

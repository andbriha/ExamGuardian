using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RosterCredentialsAndReportCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuedPasswordProtected",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportCategory",
                table: "Questions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Тестові завдання");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedPasswordProtected",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReportCategory",
                table: "Questions");
        }
    }
}

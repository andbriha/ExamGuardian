using Microsoft.EntityFrameworkCore.Migrations;

namespace DataAccessLayer.Migrations;

public partial class StudentGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("StudentGroups", columns: table => new {
            Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_StudentGroups", x => x.Name));
        migrationBuilder.Sql("""
            INSERT INTO "StudentGroups" ("Name")
            SELECT "Group" FROM "Users" WHERE "Role" = 'Student' AND "Group" IS NOT NULL AND "Group" <> '' AND length("Group") <= 100
            UNION
            SELECT "AllowedGroup" FROM "Exams" WHERE "AllowedGroup" IS NOT NULL AND "AllowedGroup" <> '' AND length("AllowedGroup") <= 100
            ON CONFLICT DO NOTHING;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("StudentGroups");
}

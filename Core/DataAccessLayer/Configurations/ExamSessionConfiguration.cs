// ExamSessionConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations;

public class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.HasKey(es => es.Id);
        builder.HasIndex(es => new { es.StudentId, es.ExamId }).IsUnique().HasFilter("\"IsFinished\" = false");

        // Зв'язок зі студентом (User)
        builder.HasOne(es => es.Student)
               .WithMany(s => s.Sessions)
               .HasForeignKey(es => es.StudentId)
               .OnDelete(DeleteBehavior.Restrict);

        // Зв'язок з іспитом
        builder.HasOne(es => es.Exam)
               .WithMany(e => e.Sessions)
               .HasForeignKey(es => es.ExamId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
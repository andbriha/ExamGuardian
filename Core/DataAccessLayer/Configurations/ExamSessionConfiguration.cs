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

        // Зв'язок зі студентом (User)
        builder.HasOne(es => es.Student)
               .WithMany(s => s.Sessions)
               .HasForeignKey(es => es.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        // Зв'язок з іспитом
        builder.HasOne(es => es.Exam)
               .WithMany(e => e.Sessions)
               .HasForeignKey(es => es.ExamId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
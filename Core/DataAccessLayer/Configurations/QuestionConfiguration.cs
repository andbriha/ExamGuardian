using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);

        // Текст питання може бути довгим, виділяємо достатньо місця
        builder.Property(q => q.Text).IsRequired().HasMaxLength(1500);

        // Зв'язок з іспитом. При видаленні іспиту — видаляються всі його питання (Cascade)
        builder.HasOne(q => q.Exam)
               .WithMany(e => e.Questions)
               .HasForeignKey(q => q.ExamId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
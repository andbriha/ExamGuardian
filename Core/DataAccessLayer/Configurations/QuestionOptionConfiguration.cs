using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations;

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.HasKey(qo => qo.Id);

        builder.Property(qo => qo.Text).IsRequired().HasMaxLength(500);

        // Зв'язок з питанням. При видаленні питання — видаляються його варіанти
        builder.HasOne(qo => qo.Question)
               .WithMany(q => q.Options)
               .HasForeignKey(qo => qo.QuestionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
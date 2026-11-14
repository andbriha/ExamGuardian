using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(250);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        // Зв'язок з творцем (User)
        builder.HasOne(e => e.Creator)
               .WithMany(u => u.CreatedExams)
               .HasForeignKey(e => e.CreatorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
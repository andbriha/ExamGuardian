using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Login).IsUnique();

        builder.Property(u => u.Login).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(50).IsRequired();
        builder.Property(u => u.RefreshToken).HasMaxLength(200).IsRequired(false);
        builder.Property(u => u.RefreshTokenExpiryTime).IsRequired(false);
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.BioVector).IsRequired(false);
        builder.Property(u => u.PhotoUrl).HasMaxLength(500).IsRequired(false);

        builder.HasMany(u => u.CreatedExams)
               .WithOne(e => e.Creator)
               .HasForeignKey(e => e.CreatorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Sessions)
               .WithOne(s => s.Student)
               .HasForeignKey(s => s.StudentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
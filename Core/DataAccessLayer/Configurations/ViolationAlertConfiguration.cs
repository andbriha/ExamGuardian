using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using DataAccessLayer.Entities;

namespace DataAccessLayer.Configurations
{
    public class ViolationAlertConfiguration : IEntityTypeConfiguration<ViolationAlert>
    {
        public void Configure(EntityTypeBuilder<ViolationAlert> builder)
        {
            builder.HasKey(va => va.Id);
            builder.HasIndex(va => va.EventId).IsUnique().HasFilter("\"EventId\" <> '00000000-0000-0000-0000-000000000000'");
            builder.HasIndex(va => new { va.ExamSessionId, va.RelativeTimestampSeconds });
            builder.Property(va => va.ViolationType).IsRequired().HasMaxLength(50);
            builder.Property(va => va.Timestamp).HasDefaultValueSql("NOW()");

            builder.Property(va => va.Confidence).IsRequired();
            builder.Property(va => va.RelativeTimestampSeconds).IsRequired();
            builder.Property(va => va.EvidenceUrl).HasMaxLength(500);

            builder.HasOne(va => va.Session)
                   .WithMany(es => es.Violations)
                   .HasForeignKey(va => va.ExamSessionId);
        }
    }
}
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
        public DbSet<RefreshCredential> RefreshCredentials => Set<RefreshCredential>();
        public DbSet<ExaminerGroupGrant> ExaminerGroupGrants => Set<ExaminerGroupGrant>();
        public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Exam> Exams => Set<Exam>();
        public DbSet<ExamSession> Sessions => Set<ExamSession>();
        public DbSet<ViolationAlert> Violations => Set<ViolationAlert>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AuthSession>().HasKey(s => s.Id);
            modelBuilder.Entity<AuthSession>().HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AuthSession>().Property(s => s.CredentialStamp).HasMaxLength(64);
            modelBuilder.Entity<AuthSession>().HasIndex(s => s.ExpiresAt);
            modelBuilder.Entity<RefreshCredential>().HasKey(s => s.Hash);
            modelBuilder.Entity<RefreshCredential>().Property(s => s.Hash).HasMaxLength(64);
            modelBuilder.Entity<RefreshCredential>().HasOne(s => s.Session).WithMany().HasForeignKey(s => s.SessionId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ExaminerGroupGrant>().HasKey(g => new {g.ExaminerId, g.Group});
            modelBuilder.Entity<ExaminerGroupGrant>().Property(g => g.Group).HasMaxLength(100);
            modelBuilder.Entity<ExaminerGroupGrant>().HasOne<User>().WithMany().HasForeignKey(g => g.ExaminerId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StudentGroup>().HasKey(g => g.Name);
            modelBuilder.Entity<StudentGroup>().Property(g => g.Name).HasMaxLength(100);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
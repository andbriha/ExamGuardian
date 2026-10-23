using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
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
            modelBuilder.Entity<StudentGroup>().HasKey(g => g.Name);
            modelBuilder.Entity<StudentGroup>().Property(g => g.Name).HasMaxLength(100);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
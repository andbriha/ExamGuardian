using BusinessLogicLayer.Constants;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Seeding;

public static class DbInitializer
{
    public static void Initialize(IServiceProvider serviceProvider, bool migrate = false)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();


        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (migrate || config.GetValue<bool?>("Database:AutoMigrate") == true || (config["Database:AutoMigrate"] is null && environment.IsDevelopment())) context.Database.Migrate();
        else if (context.Database.GetPendingMigrations().Any()) throw new InvalidOperationException("Database migration required. Run Api.dll --migrate before starting production replicas.");


        var bootstrapLogin = config["Bootstrap:ExaminerLogin"];
        if (!string.IsNullOrWhiteSpace(bootstrapLogin) && !context.Users.Any(u => u.CanManageRoster)) {
            var existingAdmin = context.Users.SingleOrDefault(u => u.Role == SystemRoles.Examiner && u.Login == bootstrapLogin);
            if (existingAdmin is not null) { existingAdmin.CanManageRoster = true; context.SaveChanges(); }
        }
        if (!context.Users.Any(u => u.Role == SystemRoles.Examiner))
        {
            var login = config["Bootstrap:ExaminerLogin"];
            var password = config["Bootstrap:ExaminerPassword"];
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
                throw new InvalidOperationException("Set Bootstrap__ExaminerLogin and Bootstrap__ExaminerPassword (at least 12 characters) for the first examiner.");
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Chief Examiner",
                Login = login,

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                CanManageRoster = true,
                Role = SystemRoles.Examiner
            });

            context.SaveChanges();
        }

        if (config.GetValue<bool>("Seed:DemoExam") && !context.Exams.Any())
        {
            var adminUser = context.Users.FirstOrDefault(u => u.Role == SystemRoles.Examiner);
            var exam = new Exam
            {
                Title = "Тестовий іспит: Комп'ютерні мережі та безпека",
                Description = "Базовий іспит для тестування системи автоматизованого прокторингу",
                RawContent = "",
                CreatorId = adminUser?.Id ?? Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ScheduledStartTime = DateTime.UtcNow,
                DurationMinutes = 60,
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "На якому рівні моделі OSI працює протокол TLS/SSL?",
                        Points = 1,
                        CorrectIndex = 1,
                        OrderIndex = 0,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Application layer (Прикладний)", OrderIndex = 0 },
                            new QuestionOption { Text = "Transport / Presentation layer (Транспортний/Представницький)", OrderIndex = 1 },
                            new QuestionOption { Text = "Network layer (Мережевий)", OrderIndex = 2 },
                            new QuestionOption { Text = "Data Link layer (Канальний)", OrderIndex = 3 },
                        }
                    },
                    new Question
                    {
                        Text = "Який тип криптографічного алгоритму використовує RSA?",
                        Points = 1,
                        CorrectIndex = 1,
                        OrderIndex = 1,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Симетричне блокове шифрування", OrderIndex = 0 },
                            new QuestionOption { Text = "Асиметричне шифрування з відкритим ключем", OrderIndex = 1 },
                            new QuestionOption { Text = "Одностороннє гешування", OrderIndex = 2 },
                            new QuestionOption { Text = "Стеганографія", OrderIndex = 3 },
                        }
                    },
                    new Question
                    {
                        Text = "Який механізм захисту використовується для запобігання атакам SQL Injection?",
                        Points = 1,
                        CorrectIndex = 0,
                        OrderIndex = 2,
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Параметризовані запити (Prepared Statements)", OrderIndex = 0 },
                            new QuestionOption { Text = "Конкатенація рядків SQL", OrderIndex = 1 },
                            new QuestionOption { Text = "Збільшення таймауту бази даних", OrderIndex = 2 },
                            new QuestionOption { Text = "Відключення індексації таблиць", OrderIndex = 3 },
                        }
                    }
                }
            };
            context.Exams.Add(exam);
            context.SaveChanges();
        }
    }
}
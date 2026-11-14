using BusinessLogicLayer.Security;
using BusinessLogicLayer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using System.Security.Claims;
using BusinessLogicLayer;
using Core.Seeding;
using DataAccessLayer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Core;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--healthcheck")) {
            try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; Environment.Exit(client.GetAsync("http://127.0.0.1:8080/health/ready").GetAwaiter().GetResult().IsSuccessStatusCode ? 0 : 1); }
            catch { Environment.Exit(1); }
            return;
        }
        var builder = WebApplication.CreateBuilder(args.Where(a => a is not ("--retention-preview" or "--retention-apply" or "--migrate")).ToArray());
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Database connection is required.");
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32) throw new InvalidOperationException("Configure a random Jwt__Key of at least 32 characters.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"])) throw new InvalidOperationException("JWT issuer and audience are required.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["AiService:ApiKey"]) || builder.Configuration["AiService:ApiKey"]!.Length < 32) throw new InvalidOperationException("Configure AiService__ApiKey.");

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.Configure<ForwardedHeadersOptions>(options => {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
        });
        builder.Services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = builder.Configuration.GetValue<int?>("Security:AuthRequestsPerMinute") ?? 120,
                    Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });
        var keysPath = builder.Configuration["CredentialKeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "state", "credential-keys");
        Directory.CreateDirectory(keysPath);
        var accessKeys = new AccessTokenKeys(Path.Combine(keysPath, "access"));
        builder.Services.AddSingleton(accessKeys);
        builder.Services.AddDataProtection().SetApplicationName("ExamGuardian").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        builder.Services.AddSingleton<Core.Security.IssuedCredentials>();
        builder.Services.AddScoped<Core.Importing.Students.RosterImportService>();
        builder.Services.AddScoped<Core.Exports.GroupResultsService>();
        builder.Services.AddSingleton<Core.Documents.GroupPdfWriter>();
        PdfSharp.Fonts.GlobalFontSettings.FontResolver = new Core.Documents.DocumentFonts();
#if EXAMGUARDIAN_DEVELOPMENT
        builder.Services.AddSingleton<Core.Development.DevelopmentAdmission>();
#endif
        builder.Services.AddScoped<Core.Exports.GradeExportService>();
        builder.Services.AddScoped<Core.Exports.GroupCredentialsService>();
        builder.Services.AddTransient<Core.Importing.Qti.IQtiItemParser, Core.Importing.Qti.ChoiceItemParser>();
        builder.Services.AddTransient<Core.Importing.Qti.QtiPackageReader>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
                if (allowedOrigins != null && allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
                else
                {
                    if (builder.Environment.IsDevelopment()) policy.WithOrigins("http://localhost:4200").AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                }
            });
        });

        // ВИПРАВЛЕНО: Додаємо JWT Security Scheme в метадані OpenAPI для Scalar
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT token"
                });

                document.SecurityRequirements = new List<OpenApiSecurityRequirement>
                {
                    new()
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    }
                };
                return Task.CompletedTask;
            });
        });

        builder.Services.AddDataAccessDependencies(builder.Configuration);
        builder.Services.AddBusinessDependencies();
        builder.Services.AddScoped<Core.Security.SessionAccess>();
        builder.Services.AddScoped<Core.Security.RosterAccess>();
        builder.Services.AddSingleton<Core.Security.MediaLinkSigner>();
        builder.Services.AddSingleton<Core.Security.AdmissionValidator>();
        builder.Services.AddSingleton<Core.Security.AttemptTokens>();
        builder.Services.AddHostedService<Core.Services.DeadlineWorker>();
        builder.Services.AddSingleton<Core.Services.VideoInspector>();
        builder.Services.AddScoped<Core.Services.RetentionMaintenance>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = accessKeys.Key,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(5),
            };
            options.Events = new JwtBearerEvents {
                OnTokenValidated = async context => {
                    var principal = context.Principal!;
                    if (!Guid.TryParse(principal.FindFirst("sid")?.Value, out var sid)) { context.Fail("Session required"); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<DataAccessLayer.Data.AppDbContext>();
                    var session = await db.AuthSessions.AsNoTracking().Include(s => s.User).SingleOrDefaultAsync(s => s.Id == sid);
                    if (session is null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow ||
                        session.UserId != Core.Security.SessionAccess.UserId(principal) || session.CredentialStamp != AuthService.Hash(session.User.PasswordHash) ||
                        principal.FindFirst(ClaimTypes.Role)?.Value != session.User.Role) context.Fail("Session revoked");
                }
            };
        });

        var app = builder.Build();

        if (args.Contains("--retention-preview") || args.Contains("--retention-apply")) {
            using var scope=app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<Core.Services.RetentionMaintenance>().RunAsync(args.Contains("--retention-apply")).GetAwaiter().GetResult();
            return;
        }

        DbInitializer.Initialize(app.Services, args.Contains("--migrate"));
        if (args.Contains("--migrate")) return;

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseExceptionHandler();
        app.UseForwardedHeaders();
        app.UseRateLimiter();
        app.MapGet("/.well-known/jwks.json", () => Results.Ok(accessKeys.PublicJwks()));
        if (builder.Configuration.GetValue<bool>("Https:Redirect")) app.UseHttpsRedirection();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/health/ready", async (DataAccessLayer.Data.AppDbContext db) =>
            await db.Database.CanConnectAsync() ? Results.Ok(new { ready = true }) : Results.StatusCode(503));
        app.Run();
    }
}

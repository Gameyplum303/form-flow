using System.Threading.RateLimiting;
using FormFlow.Backend;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Email;
using FormFlow.Backend.Endpoints;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

using LiteDB;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Register LiteDB as a shared service
builder.Services.AddSingleton<ILiteDatabase>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("LiteDb") ?? "Filename=formflow.db;Connection=shared";
    return new LiteDatabase(connectionString);
});

builder.Services.AddSingleton<IQuestionRepository, QuestionRepository>();
builder.Services.AddSingleton<ISurveyRepository, SurveyRepository>();
builder.Services.AddSingleton<IResponseRepository, ResponseRepository>();
builder.Services.AddSingleton<QuestionValidator>();
builder.Services.AddSingleton<QuestionValidationEngine>();
builder.Services.AddSingleton<ResponseValidator>();
builder.Services.AddSingleton<DatabaseSeeder>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddSingleton<AdminAccountSeeder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IAccountTokenRepository, AccountTokenRepository>();

// Emails for verifying addresses and resetting passwords go through SMTP when Email:Smtp:Host is set,
// and to an in-memory outbox otherwise.
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConfiguration>().GetSection(EmailSettings.Section).Get<EmailSettings>() ?? new EmailSettings());
builder.Services.AddSingleton<OutboxEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailSettings>() is { UsesSmtp: true } settings
    ? new SmtpEmailSender(settings)
    : sp.GetRequiredService<OutboxEmailSender>());
builder.Services.AddSingleton<AccountEmails>();

// Admin endpoints need a bearer token from POST /api/auth/login; taking surveys stays anonymous.
var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Startup");
var jwt = JwtSettings.FromConfiguration(builder.Configuration, startupLogger);
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwt.ValidationParameters();
        options.TokenValidationParameters.NameClaimType = "unique_name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var id = CurrentUser.From(context.Principal!).Id;
                var user = id is { } userId ? context.HttpContext.RequestServices.GetRequiredService<IUserRepository>().FindById(userId) : null;
                if (!SessionCheck.IsCurrent(user, context.SecurityToken.ValidFrom))
                {
                    context.Fail("This sign-in has ended. Please sign in again.");
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(JwtSettings.AdminPolicy, policy => policy.RequireRole(Roles.Admin))
    .AddPolicy(JwtSettings.BuilderPolicy, policy => policy.RequireRole(Roles.Admin, Roles.Professor))
    .AddPolicy(JwtSettings.SignedInPolicy, policy => policy.RequireRole(Roles.All));

// Limits per client address, to slow down password guessing and spam submissions.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.LoginRateLimit, context => FixedWindowPerClient(context,
        builder.Configuration.GetValue("RateLimits:LoginPerMinute", 20)));
    options.AddPolicy(AuthEndpoints.AccountRateLimit, context => FixedWindowPerClient(context,
        builder.Configuration.GetValue("RateLimits:AccountPerMinute", 20)));
    options.AddPolicy(ResponseEndpoints.SubmitRateLimit, context => FixedWindowPerClient(context,
        builder.Configuration.GetValue("RateLimits:SubmissionsPerMinute", 60)));
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerTokenSecurity());
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
    });
});

var app = builder.Build();

app.Services.GetRequiredService<DatabaseSeeder>().Seed();
app.Services.GetRequiredService<AdminAccountSeeder>().Seed();
app.Services.GetRequiredService<ISurveyRepository>().AssignMissingShareCodes();

// The API docs are part of the demo, so they are served in every environment.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "FormFlow API");
    options.RoutePrefix = "swagger";
});

if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapQuestionEndpoints();
app.MapSurveyEndpoints();
app.MapResponseEndpoints();

app.Run();

static RateLimitPartition<string> FixedWindowPerClient(HttpContext context, int permitsPerMinute) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;

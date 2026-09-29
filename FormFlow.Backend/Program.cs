using FormFlow.Backend;
using FormFlow.Backend.Endpoints;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Services;

using LiteDB;

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

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
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

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapQuestionEndpoints();
app.MapSurveyEndpoints();
app.MapResponseEndpoints();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;

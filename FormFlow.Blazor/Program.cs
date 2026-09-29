using FormFlow.Blazor.Components;
using FormFlow.Blazor.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Every API client talks to the same backend.
var backendApi = builder.Configuration["BackendAPI:BaseUrl"] is { Length: > 0 } baseUrl
    ? new Uri(baseUrl)
    : throw new InvalidOperationException("BackendApi:BaseUrl is not configured.");
void UseBackendApi(HttpClient client) => client.BaseAddress = backendApi;

builder.Services.AddHttpClient<IQuestionService, QuestionService>(UseBackendApi);
builder.Services.AddHttpClient<ISurveyService, SurveyService>(UseBackendApi);
builder.Services.AddHttpClient<IAuthService, AuthService>(UseBackendApi);
builder.Services.AddHttpClient<IAccountService, AccountService>(UseBackendApi);

// The signed-in admin for this circuit; the services above add its token to API calls.
builder.Services.AddScoped<AdminSession>();

// Someone taking surveys without an account, remembered per browser.
builder.Services.AddScoped<IRespondentIdentity, RespondentIdentity>();

// Answers not yet submitted, kept in the browser so a respondent can come back to them.
builder.Services.AddScoped<ISurveyDrafts, SurveyDrafts>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Containers and proxies that terminate TLS serve plain HTTP.
if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

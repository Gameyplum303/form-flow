using FormFlow.Blazor.Components;
using FormFlow.Blazor.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddHttpClient<IQuestionService, QuestionService>(client =>
{
    var url = builder.Configuration["BackendAPI:BaseUrl"];
    client.BaseAddress = new Uri(url ?? throw new Exception("URL Missing!"));
});

builder.Services.AddHttpClient<ISurveyService, SurveyService>(client =>
{
    var url = builder.Configuration["BackendAPI:BaseUrl"];
    client.BaseAddress = new Uri(url ?? throw new Exception("URL Missing!"));
});

// The signed-in admin for this circuit; the services above add its token to API calls.
builder.Services.AddScoped<AdminSession>();

// Someone taking surveys without an account, remembered per browser.
builder.Services.AddScoped<IRespondentIdentity, RespondentIdentity>();

builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
{
    var url = builder.Configuration["BackendAPI:BaseUrl"];
    client.BaseAddress = new Uri(url ?? throw new Exception("URL Missing!"));
});

builder.Services.AddHttpClient<IAccountService, AccountService>(client =>
{
    var url = builder.Configuration["BackendAPI:BaseUrl"];
    client.BaseAddress = new Uri(url ?? throw new Exception("URL Missing!"));
});

builder.Services.AddHttpClient("AdminApi", client =>
{
    var url = builder.Configuration["BackendAPI:BaseUrl"];
    client.BaseAddress = new Uri(url ?? throw new Exception("URL Missing!"));
});

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

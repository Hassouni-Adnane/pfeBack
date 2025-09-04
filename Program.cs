var builder = WebApplication.CreateBuilder(args);

// 1. Configure Console logging at Debug for our controller
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter(
    "SignNowBackend.Controllers.DocumentController",
    LogLevel.Debug);

// 2. Register CORS, HttpClient, MVC controllers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// 3. Build the app BEFORE you reference 'app'
var app = builder.Build();

// 4. Only use HTTPS redirection outside of Development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 5. Always enable CORS and map controllers
app.UseCors();
app.MapControllers();

app.Run();

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignNowBackend.Services;   // 👈 add this

var builder = WebApplication.CreateBuilder(args);

// 1) Logging (update the category if you renamed controllers)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("SignNowBackend", LogLevel.Debug);

// 2) Services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000") // add others if needed
              .AllowAnyHeader()
              .AllowAnyMethod());
});
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// 👇 register your app services so controllers can resolve them
builder.Services.AddScoped<SignNowService>();
builder.Services.AddScoped<NodeReporter>();

// 3) Build
var app = builder.Build();

// 4) Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.MapControllers();

app.Run();

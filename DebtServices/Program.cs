using DebtServices.Database;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using DebtServices.Services;
using Microsoft.EntityFrameworkCore;
using System.Configuration;

var builder = WebApplication.CreateBuilder(args);

var configFilePath = Environment.GetEnvironmentVariable("DS_CONFIG_PATH");
if (!string.IsNullOrEmpty(configFilePath))
{
    builder.Host.ConfigureAppConfiguration((hostingContext, appConfiguration) =>
    {
        appConfiguration.AddJsonFile(configFilePath);
    });
};

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://*:5170");
}

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddDbContextFactory<DebtReminderContext>(options => options.UseCosmos(builder.Configuration.GetConnectionString("DebtReminderContext"), "DebtReminder"));

builder.Services.Configure<WeComConfiguration>(builder.Configuration.GetSection("WeCom"));
builder.Services.Configure<CosmosDbConfiguration>(builder.Configuration.GetSection("CosmosDb"));

builder.Services.AddScoped<CosmosDbService>();
builder.Services.AddScoped<WeComService>();
builder.Services.AddScoped<DebtSubscriptionService>();
builder.Services.AddScoped<EastmoneyService>();

builder.Services.AddHostedService<NotificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();

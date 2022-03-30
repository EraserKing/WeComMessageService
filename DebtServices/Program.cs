using DebtServices.Database;
using DebtServices.Models.Configurations;
using DebtServices.Processors;
using DebtServices.Processors.Interfaces;
using DebtServices.Services;
using Microsoft.EntityFrameworkCore;

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

builder.Services.Configure<WeComServicesConfiguration>(builder.Configuration.GetSection("WeComServices"));
builder.Services.Configure<DebtServiceConfiguration>(builder.Configuration.GetSection("DebtService"));
builder.Services.Configure<CosmosDbConfiguration>(builder.Configuration.GetSection("CosmosDb"));

builder.Services.AddScoped<CosmosDbService>();
builder.Services.AddScoped<WeComService>();
builder.Services.AddScoped<DebtSubscriptionService>();
builder.Services.AddScoped<EastmoneyService>();

builder.Services.AddScoped<IProcessor, DebtMessageProcessor>();
builder.Services.AddScoped<DebtMessageProcessor>();

builder.Services.AddHostedService<NotificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();

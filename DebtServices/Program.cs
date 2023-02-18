using DebtServices.Database;
using DebtServices.Models;
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

builder.Services.AddDbContextPool<DebtReminderContext>(options => options.UseCosmos(builder.Configuration.GetConnectionString("DebtReminderContext"), "DebtReminder"));

builder.Services.Configure<WeComServicesConfiguration>(builder.Configuration.GetSection("WeComServices"));
builder.Services.Configure<DebtServiceConfiguration>(builder.Configuration.GetSection("DebtService"));
builder.Services.Configure<QinglongServiceConfiguration>(builder.Configuration.GetSection("QinglongService"));
builder.Services.Configure<MikanServiceConfiguration>(builder.Configuration.GetSection("MikanService"));

builder.Services.AddScoped<CosmosDbService<DebtReminderContext, DebtReminderModel>>();
builder.Services.AddScoped<WeComService>();
builder.Services.AddScoped<DebtSubscriptionService>();
builder.Services.AddScoped<EastmoneyService>();
builder.Services.AddScoped<QinglongService>();
builder.Services.AddScoped<MikanService>();

builder.Services.AddScoped<IProcessor, DebtMessageProcessor>();
builder.Services.AddScoped<DebtMessageProcessor>();

builder.Services.AddScoped<IProcessor, QinglongProcessor>();
builder.Services.AddScoped<QinglongProcessor>();

builder.Services.AddScoped<IProcessor, MikanProcessor>();
builder.Services.AddScoped<MikanProcessor>();

builder.Services.AddHostedService<NotificationService>();
builder.Services.AddSingleton<MikanBackgroundService>();
builder.Services.AddHostedService<MikanBackgroundService>(provider => provider.GetService<MikanBackgroundService>());

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();

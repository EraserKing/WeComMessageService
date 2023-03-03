using DebtReminder.Database;
using DebtReminder.Models;
using DebtReminder.Models.Configurations;
using DebtReminder.Processors;
using DebtReminder.Services;
using Eastmoney.Services;
using Microsoft.EntityFrameworkCore;
using Mikan.Models.Configurations;
using Mikan.Processors;
using Mikan.Services;
using Qbittorrent.Models.Configurations;
using Qbittorrent.Processors;
using Qbittorrent.Services;
using Qinglong.Models.Configurations;
using Qinglong.Processors;
using Qinglong.Services;
using WeComCommon.Models.Configurations;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

var builder = WebApplication.CreateBuilder(args);

var configSource = Environment.GetEnvironmentVariable("WCM_CONFIG_SOURCE") ?? "AZURE";
switch (configSource)
{
    case "AZURE":
        Console.WriteLine("Reading configuration from Azure");
        builder.Configuration.AddAzureAppConfiguration(Environment.GetEnvironmentVariable("WCM_CONFIG_SOURCE_AZURE"));
        break;

    case "FILE":
        Console.WriteLine("Reading configuration from file");
        builder.Host.ConfigureAppConfiguration((hostingContext, appConfiguration) =>
        {
            appConfiguration.AddJsonFile(Environment.GetEnvironmentVariable("WCM_CONFIG_SOURCE_FILE"));
        });
        break;
}

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
builder.Services.Configure<QbittorrentServiceConfiguration>(builder.Configuration.GetSection("QbittorrentService"));

builder.Services.AddSingleton<WeComService>();

builder.Services.AddScoped<CosmosDbService<DebtReminderContext, DebtReminderModel>>();
builder.Services.AddScoped<DebtSubscriptionService>();
builder.Services.AddScoped<EastmoneyService>();
builder.Services.AddScoped<QinglongService>();
builder.Services.AddScoped<MikanService>();
builder.Services.AddScoped<QbittorrentService>();

builder.Services.AddScoped<IProcessor, DebtMessageProcessor>();
builder.Services.AddScoped<DebtMessageProcessor>();

builder.Services.AddScoped<IProcessor, QinglongProcessor>();
builder.Services.AddScoped<QinglongProcessor>();

builder.Services.AddScoped<IProcessor, MikanProcessor>();
builder.Services.AddScoped<MikanProcessor>();

builder.Services.AddScoped<IProcessor, QbittorrentProcessor>();
builder.Services.AddScoped<QbittorrentProcessor>();

builder.Services.AddHostedService<NotificationService>();
builder.Services.AddSingleton<MikanBackgroundService>();
builder.Services.AddHostedService(provider => provider.GetService<MikanBackgroundService>());

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();

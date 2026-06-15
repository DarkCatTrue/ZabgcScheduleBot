using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.Bot;
using ZabgcScheduleBot.Parsing;
using ZabgcScheduleBot.Services;

DotNetEnv.Env.Load();

FileSystem file = new FileSystem();
file.InitialCatalogs();

Parse parse = new Parse(file);
await parse.SaveAllData();

string token = DotNetEnv.Env.GetString("VkToken");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<VkApi>(sp =>
{
    var api = new VkApi();
    api.Authorize(new ApiAuthParams { AccessToken = token });
    return api;
});

builder.WebHost.UseUrls("http://*:5004");
builder.Services.AddSingleton<Finder>();
builder.Services.AddSingleton<FileSystem>();
builder.Services.AddSingleton<Parse>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddHttpClient<ApiClient>();

builder.Services.AddSingleton<WebhookBufferService>();
builder.Services.AddHostedService<WebhookBufferService>(sp => sp.GetRequiredService<WebhookBufferService>());

builder.Services.AddHostedService<VkBot>();                      
builder.Services.AddHostedService<UpdateCheckerBackgroundService>(); 

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers(); 

await app.RunAsync();
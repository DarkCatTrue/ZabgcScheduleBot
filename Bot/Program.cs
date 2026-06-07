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

Parse parse = new Parse();
await parse.SaveAllData(isFirstTime:true);

string token = DotNetEnv.Env.GetString("VkToken");


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
    services.AddSingleton(sp =>
    {
        var api = new VkApi();
        api.Authorize(new ApiAuthParams
        {
            AccessToken = token
        });
        return api;
    });
        
        services.AddSingleton<Parse>();
        services.AddSingleton<Finder>();
        services.AddSingleton<FileSystem>();
        services.AddSingleton<HttpClient>();
        services.AddHttpClient<ApiClient>();
        services.AddSingleton<NotificationService>();
        services.AddHostedService<VkBot>();
        services.AddHostedService<UpdateCheckerBackgroundService>();
    })
    .Build();

await host.RunAsync();


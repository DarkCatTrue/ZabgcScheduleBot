using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.Bot;
using ZabgcScheduleBot.Parsing;

FileSystem file = new FileSystem();
file.InitialCatalogs();
 
DotNetEnv.Env.Load();
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

        services.AddSingleton<NotificationService>();
        services.AddHostedService<VkBot>();
    })
    .Build();

await host.RunAsync();


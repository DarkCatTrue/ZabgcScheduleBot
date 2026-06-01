using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.Bot;
using ZabgcScheduleBot.Parsing;

FileSystem file = new FileSystem();
file.InitialCatalogs();

DotNetEnv.Env.Load();

//Parse parse = new Parse();
//await parse.SaveAllData();
//string pena = await parse.GetScheduleFromFile("PreviousSchedule\\GroupsSchedule\\cg76.htm");
//Console.WriteLine(pena);
//await parse.SaveSchedulePages("Jsons\\Groups.json", "CurrentSchedule\\GroupsSchedule");
//await parse.SaveSchedulePages("Jsons\\Teachers.json", "CurrentSchedule\\TeachersSchedule");
//await parse.SaveSchedulePages("Jsons\\Audiences.json", "CurrentSchedule\\AudiencesSchedule");

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
        services.AddSingleton<Parse>();
        services.AddSingleton<Finder>();
        services.AddSingleton<FileSystem>();
        services.AddSingleton<HttpClient>();
        services.AddHttpClient<ApiClient>();
        services.AddHostedService<VkBot>();
    })
    .Build();

await host.RunAsync();


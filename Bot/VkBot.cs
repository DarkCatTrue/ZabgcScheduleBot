using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.API.DTOs;
using ZabgcScheduleBot.Parsing;

namespace ZabgcScheduleBot.Bot
{

    public class VkBot : BackgroundService
    {
        private readonly VkApi _vkApi;
        private readonly ILogger<VkBot> _logger;
        private readonly NotificationService _notificationService;
        private readonly Finder _finder;
        private readonly ApiClient _apiClient;
        private readonly Parse _parse;
        private readonly FileSystem _fileSystem;


        private static readonly ConcurrentDictionary<long, UserDialogState> _userStates = new();

        public VkBot(VkApi vkApi, ILogger<VkBot> logger, NotificationService notificationService, Finder finder, ApiClient apiClient, Parse parse, FileSystem fileSystem)
        {
            _vkApi = vkApi;
            _logger = logger;
            _notificationService = notificationService;
            _finder = finder;
            _apiClient = apiClient;
            _fileSystem = fileSystem;  
            _parse = parse;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("VK Bot запущен.");
            var groupId = await GetGroupIdAsync(stoppingToken);
            await RunLongPollLoopAsync(groupId, stoppingToken);
        }

        private async Task<ulong?> GetGroupIdAsync(CancellationToken stoppingToken)
        {
            try
            {
                var groups = await _vkApi.Groups.GetByIdAsync(null, null, null);
                if (groups != null && groups.Count > 0)
                    return (ulong?)groups[0].Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось получить groupId (возможно, используется токен пользователя)");
            }
            return null;
        }

        private async Task RunLongPollLoopAsync(ulong? groupId, CancellationToken stoppingToken)
        {
            using var httpClient = new HttpClient();
            long ts = 0;
            string key = "";
            string server = "";

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (string.IsNullOrEmpty(server))
                    {
                        (server, key, ts) = await GetLongPollServerAsync(groupId, stoppingToken);
                    }

                    string url = BuildLongPollUrl(server, key, ts);
                    var response = await httpClient.GetStringAsync(url, stoppingToken);
                    var obj = JObject.Parse(response);

                    if (obj["failed"] != null)
                    {
                        server = "";
                        continue;
                    }

                    ts = obj["ts"].Value<long>();
                    var updates = obj["updates"] as JArray;
                    if (updates != null)
                    {
                        foreach (var update in updates)
                        {
                            await ProcessUpdateAsync(update, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в LongPoll");
                    server = "";
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task<(string server, string key, long ts)> GetLongPollServerAsync(ulong? groupId, CancellationToken stoppingToken)
        {
            var lpServer = await _vkApi.Messages.GetLongPollServerAsync(
                needPts: false,
                lpVersion: 3,
                groupId: groupId,
                token: stoppingToken);
            return (lpServer.Server, lpServer.Key, (long)lpServer.Ts);
            
        }

        private static string BuildLongPollUrl(string server, string key, long ts)
        {
            return server.StartsWith("http")
                ? $"{server}?act=a_check&key={key}&ts={ts}&wait=25&mode=2"
                : $"https://{server}?act=a_check&key={key}&ts={ts}&wait=25&mode=2";
        }

        private async Task ProcessUpdateAsync(JToken update, CancellationToken stoppingToken)
        {
            if (update.Type != JTokenType.Array || update[0]?.Value<int>() != 4)
                return;

            int flags = update[2]?.Value<int>() ?? 0;
            if ((flags & 2) != 0)
                return;

            long fromId = update[3].Value<long>();
            string text = update[6]?.Value<string>() ?? "";
            bool isReply = update[7] is JObject extra && extra["reply"] != null;

            if (_userStates.TryGetValue(fromId, out var state) && state.Step != DialogStep.None && isReply)
            {
                switch (state.Step)
                {
                    case DialogStep.WaitingForSubscriptionGroup:
                        await ProcessSubscriptionInput(fromId, text, stoppingToken);
                        break;
                    case DialogStep.WaitingForScheduleGroup:
                        await ProcessScheduleInput(fromId, text, false, stoppingToken);
                        break;
                }
                _userStates.TryRemove(fromId, out _);
                return;
            }

            _logger.LogInformation($"Update from VK: {update}");
            
            switch (text)
            {
                case "/sub" or "Начать":
                    await StartSubscriptionFlow(fromId, stoppingToken);
                    break;
                case "/unsub":
                    await DeleteSubscribe(fromId, stoppingToken);
                    break;
                case "/previous":
                    await StartScheduleFlow(fromId, stoppingToken);
                    break;
                default:
                    _logger.LogInformation($"Ignored message from {fromId}: {text}");
                    break;
            }
        }

        // Ввод данных для просмотра предыдущего расписания
        private async Task StartScheduleFlow(long userId, CancellationToken ct)
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = "Для просмотра предыдущего расписания, ответьте на это сообщение и введите один из параметров:\n1. Название группы\n2. ФИО преподавателя\n3. Номер аудитории\n",
                RandomId = new Random().Next()
            });

            _userStates[userId] = new UserDialogState { Step = DialogStep.WaitingForScheduleGroup };
        }

        // Ввод данных для подписки 
        private async Task StartSubscriptionFlow(long userId, CancellationToken ct)
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = "Для подписки на рассылку, ответьте на это сообщение и введите один из параметров:\n1. Название группы\n2. ФИО преподавателя\n3. Номер аудитории\n",
                RandomId = new Random().Next()
            });

            _userStates[userId] = new UserDialogState { Step = DialogStep.WaitingForSubscriptionGroup };
        }

        // Метод на отправку расписания через имя файла
        private async Task ProcessScheduleInput(long userId, string descriptionName, bool isCurrentSchedule, CancellationToken ct)
        {
            var (fileName, scheduleType) = await _fileSystem.GetFileNameFromDescriptionName(descriptionName, isCurrentSchedule);

            if (scheduleType != FileSystem.ScheduleType.None && !string.IsNullOrEmpty(fileName))
            {
                var schedule = await _parse.GetScheduleFromFile(fileName, (Parse.ScheduleType)scheduleType);
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = schedule,
                    RandomId = new Random().Next()
                });
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = "Данная группа, преподаватель или аудитория не найдены.\nПовторите попытку используя команду: /previous",
                    RandomId = new Random().Next()
                });
            }
        }
        
        
        // Подписка на уведомления
        private async Task ProcessSubscriptionInput(long userId, string input, CancellationToken ct)
        {
            bool found = await _finder.Find(input);
            var user = await _apiClient.GetUserByChatIdAsync(userId.ToString());
            
            if (user == null)
            {
                if (found)
                {
                    var newUser = new UsersDto
                    {
                        ChatId = userId.ToString(),
                        DescriptionName = input,
                        PlatformName = PlatformType.VK.ToString()
                    };

                    await _apiClient.CreateUserAsync(newUser);

                    await _vkApi.Messages.SendAsync(new MessagesSendParams
                    {
                        UserId = userId,
                        Message = $"Вы успешно подписались на обновления: {input}",
                        RandomId = new Random().Next()
                    });
                }
                else
                {
                    await _vkApi.Messages.SendAsync(new MessagesSendParams
                    {
                        UserId = userId,
                        Message = "Не найдено название группы, ФИО преподавателя или аудитории с таким названием.\nПопробуйте снова командой /sub",
                        RandomId = new Random().Next()
                    });
                }
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = $"Вы не можете быть подписаны на {input}, поскольку вы уже подписаны на обновления: {user.DescriptionName}.\nДля отмены подписки введите команду /unsub",
                    RandomId = new Random().Next()
                });
            }
        }

        // Отписка от уведомлений
        private async Task DeleteSubscribe(long userId, CancellationToken stoppingToken)
        {
            bool userdeleted = await _apiClient.DeleteUserAsync(userId);
            if (userdeleted)
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = "Вы успешно отписались от рассылки уведомлений.",
                    RandomId = new Random().Next()
                });
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = "Неудалось отписать вас от рассылки. Возможно вы не были подписаны на рассылку.",
                    RandomId = new Random().Next()
                });
            }
        }

        private enum DialogStep
        {
            None,
            WaitingForSubscriptionGroup,
            WaitingForScheduleGroup
        }

        private class UserDialogState
        {
            public DialogStep Step { get; set; }
            public string TempData { get; set; }
        }

    }
}

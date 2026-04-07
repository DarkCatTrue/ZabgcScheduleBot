using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;

namespace ZabgcScheduleBot.Bot
{

    public class VkBot : BackgroundService
    {
        private readonly VkApi _vkApi;
        private readonly ILogger<VkBot> _logger;
        private readonly NotificationService _notificationService;

        private static readonly ConcurrentDictionary<long, UserDialogState> _userStates = new();

        public VkBot(VkApi vkApi, ILogger<VkBot> logger, NotificationService notificationService)
        {
            _vkApi = vkApi;
            _logger = logger;
            _notificationService = notificationService;
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

            if (_userStates.TryGetValue(fromId, out var state) && state.AwaitingInput && isReply)
            {
                await ProcessSubscriptionInput(fromId, text, stoppingToken);
                _userStates.TryRemove(fromId, out _);
                return;
            }

            switch (text)
            {
                case "/sub" or "Начать":
                    await StartSubscriptionFlow(fromId, stoppingToken);
                    break;
                case "/unsub":
                    await DeleteSubscribe(fromId, stoppingToken);
                    break;
                case "/pastSchedule":
                    await CheckPastSchedule(fromId, stoppingToken);
                    break;
                default:
                    _logger.LogInformation($"Ignored message from {fromId}: {text}");
                    break;
            }
        }

        private async Task CheckPastSchedule(long userId, CancellationToken stoppingToken)
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = "Введите название группы или ФИО преподавателя.\nПример названия группы: ИСиП-22-1.\nПример ФИО преподавателя: Зимин Ю.С.",
                RandomId = new Random().Next()
            });

            _userStates[userId] = new UserDialogState { AwaitingInput = true };
        }

        private async Task DeleteSubscribe(long userId, CancellationToken stoppingToken)
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = "Вы успешно отписались от рассылки уведомлений.",
                RandomId = new Random().Next()
            });
        }

        private async Task StartSubscriptionFlow(long userId, CancellationToken ct)
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = "Введите название группы или ФИО преподавателя.\nПример названия группы: ИСиП-22-1.\nПример ФИО преподавателя: Зимин Ю.С.",
                RandomId = new Random().Next()
            });

            _userStates[userId] = new UserDialogState { AwaitingInput = true };
        }

        private async Task ProcessSubscriptionInput(long userId, string input, CancellationToken ct)
        {
            bool found = !string.IsNullOrWhiteSpace(input);

            if (found)
            {
                await _notificationService.SendToUserAsync(PlatformType.VK, userId.ToString(),
                    $"Вы успешно подписались на обновления: {input}", ct);
            }
            else
            {
                await _notificationService.SendToUserAsync(PlatformType.VK, userId.ToString(),
                    "Не найдено название группы или ФИО преподавателя с таким названием. Попробуйте снова командой /sub.", ct);
            }
        }

        private class UserDialogState
        {
            public bool AwaitingInput { get; set; }
            public long PromptConversationMessageId { get; set; }
        }

    }
}

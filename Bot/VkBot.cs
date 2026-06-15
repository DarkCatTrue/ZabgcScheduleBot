using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sprache;
using System.Collections.Concurrent;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.API.DTOs;
using ZabgcScheduleBot.Parsing;
using ZabgcScheduleBot.Services;
using Parse = ZabgcScheduleBot.Parsing.Parse;

namespace ZabgcScheduleBot.Bot
{

    public class VkBot : BackgroundService
    {
        private readonly VkApi _vkApi;
        private readonly ILogger<VkBot> _logger;
        private readonly Finder _finder;
        private readonly ApiClient _apiClient;
        private readonly Parse _parse;
        private readonly FileSystem _fileSystem;


        private static readonly ConcurrentDictionary<long, UserDialogState> _userStates = new();

        public VkBot(VkApi vkApi, ILogger<VkBot> logger, Finder finder, ApiClient apiClient, Parse parse, FileSystem fileSystem)
        {
            _vkApi = vkApi;
            _logger = logger;
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
                case "Начать":
                    await StartTextFlow(fromId, stoppingToken);
                    break;

                case "Подписаться на рассылку":
                    await StartSubscriptionFlow(fromId, stoppingToken);
                    break;

                case "Отписаться от рассылки":
                    await DeleteSubscribe(fromId, stoppingToken);
                    break;

                case "Назад":
                    state = _userStates.GetOrAdd(fromId, _ => new UserDialogState());
                    state.CurrentMenu = MenuType.Main;
                    state.Step = DialogStep.None;
                    await SendMainKeyboard(fromId, "Вы вернулись в главное меню:", stoppingToken);
                    break;

                case "Предыдущее расписание":
                    await StartScheduleFlow(fromId, stoppingToken);
                    break;

                case "Управление уведомлениями":
                    state = _userStates.GetOrAdd(fromId, _ => new UserDialogState());
                    state.CurrentMenu = MenuType.SubscriptionManagement;
                    await SendSubscriptionManagementKeyboard(fromId, "Выберите действие:", stoppingToken);
                    break;

                default:
                    _logger.LogInformation($"Ignored message from {fromId}: {text}");
                    break;
            }
        }

        private async Task SendMainKeyboard(long peerId, string message, CancellationToken ct)
        {
            var keyboard = new MessageKeyboard
            {
                Buttons = new List<List<MessageKeyboardButton>>
        {
            new List<MessageKeyboardButton>
            {
                new MessageKeyboardButton
                {
                    Action = new MessageKeyboardButtonAction
                    {
                        Type = KeyboardButtonActionType.Text,
                        Label = "Управление уведомлениями"
                    },
                    Color = KeyboardButtonColor.Primary
                }
            },
            new List<MessageKeyboardButton>
            {
                new MessageKeyboardButton
                {
                    Action = new MessageKeyboardButtonAction
                    {
                        Type = KeyboardButtonActionType.Text,
                        Label = "Предыдущее расписание"
                    },
                    Color = KeyboardButtonColor.Default
                }
            }
        },
                OneTime = false
            };

            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                PeerId = peerId,
                Message = message,
                RandomId = new Random().Next(),
                Keyboard = keyboard
            });
        }

        private async Task SendSubscriptionManagementKeyboard(long peerId, string message, CancellationToken ct)
        {
            var keyboard = new MessageKeyboard
            {
                Buttons = new List<List<MessageKeyboardButton>>
        {
            new List<MessageKeyboardButton>
            {
                new MessageKeyboardButton
                {
                    Action = new MessageKeyboardButtonAction
                    {
                        Type = KeyboardButtonActionType.Text,
                        Label = "Подписаться на рассылку"
                    },
                    Color = KeyboardButtonColor.Positive
                }
            },
            new List<MessageKeyboardButton>
            {
                new MessageKeyboardButton
                {
                    Action = new MessageKeyboardButtonAction
                    {
                        Type = KeyboardButtonActionType.Text,
                        Label = "Отписаться от рассылки"
                    },
                    Color = KeyboardButtonColor.Negative
                }
            },
            new List<MessageKeyboardButton>
            {
                new MessageKeyboardButton
                {
                    Action = new MessageKeyboardButtonAction
                    {
                        Type = KeyboardButtonActionType.Text,
                        Label = "Назад"
                    },
                    Color = KeyboardButtonColor.Default
                }
            }
        },
                OneTime = false
            };

            await Task.Delay(200);
            await _vkApi.Messages.SendAsync(new MessagesSendParams
            {
                PeerId = peerId,
                Message = message,
                RandomId = new Random().Next(),
                Keyboard = keyboard
            });
        }

        // Ввод данных для просмотра предыдущего расписания
        private async Task StartScheduleFlow(long userId, CancellationToken ct)
        {
            await SendMainKeyboard(userId,
                "Для просмотра предыдущего расписания, нажмите «Ответить» на моё сообщение и введите один из параметров на выбор:" +
                "\n\n1. Название группы" +
                "\n2. ФИО преподавателя" +
                "\n3. Номер аудитории" +
                "\n\nНиже представлены примеры ответов на это сообщение:" +
                "\nМД-23-1" +
                "\nЗимин Ю.С." +
                "\n311"
                ,
                ct);
            _userStates[userId] = new UserDialogState { Step = DialogStep.WaitingForScheduleGroup };
        }

        // Ввод данных для подписки 
        private async Task StartSubscriptionFlow(long userId, CancellationToken ct)
        {
            var user = await _apiClient.GetUserByChatIdAsync(userId.ToString());
            if (user == null)
            {
                await SendMainKeyboard(userId,
                    "Для подписки на рассылку уведомлений, нажмите «Ответить» на моё сообщение и введите один из параметров на выбор:" +
                    "\n\n1. Название группы" +
                    "\n2. ФИО преподавателя" +
                    "\n3. Номер аудитории" +
                    "\n\nНиже представлены примеры ответов на это сообщение:" +
                    "\nМД-23-1" +
                    "\nЗимин Ю.С." +
                    "\n311",
                    ct);
                _userStates[userId] = new UserDialogState { Step = DialogStep.WaitingForSubscriptionGroup };
            }
            else
            {
                await Task.Delay(200);
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = $"Вы не можете подписаться на рассылку уведомлений, поскольку вы уже подписаны на обновления: {user.DescriptionName}.\nДля подписки на новую рассылку, необходимо отписаться от предыдущей",
                    RandomId = new Random().Next()
                });
            }
        }

        // Приветственное слово бота.
        private async Task StartTextFlow(long userId, CancellationToken ct)
        {
            await SendMainKeyboard(userId,
                "Привет! Это бот расписания ЗабГК. " +
                "\nА это то, что я умею:" +
                "\n\n1. Отправлять новое и изменённое расписание занятий и экзаменов для групп, преподавателей и аудиторий" +
                "\n2. Показывать предыдущее расписание занятий для групп, преподавателей и аудиторий." +
                "\n\nДля начала моей работы необходимо перейти в -> Управление уведомлениями -> Подписаться на уведомления. Это позволит мне присылать актуальное расписание в этот чат." +
                "\n\nСтоит отметить, что я реагирую только если вы нажали кнопку «Ответить» конкретно на моё сообщение, в ином случае все сообщения в чате будут проигнорированы."
                , ct);
        }

        // Метод для просмотра старого расписания
        private async Task ProcessScheduleInput(long userId, string descriptionName, bool isCurrentSchedule, CancellationToken ct)
        {
            await Task.Delay(200);
            var (fileName, scheduleType) = await _fileSystem.GetFullPathFromDescriptionName(descriptionName, isCurrentSchedule);

            if (scheduleType != FileSystem.ScheduleType.None && !string.IsNullOrEmpty(fileName))
            {
                var schedule = await _parse.GetScheduleFromFile(fileName);
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
                    Message = "Данная группа, преподаватель или аудитория не найдены в расписании ЗабГК." +
                    "\nЯ ищу только те названия, которые полностью совпадают по каждому символу в расписании.",
                    RandomId = new Random().Next()
                });
            }
        }


        // Подписка на уведомления
        private async Task ProcessSubscriptionInput(long userId, string input, CancellationToken ct)
        {
            await Task.Delay(200);
            bool found = await _finder.Find(input);

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
                    Message = $"Вы успешно подписались на уведомления: {input}",
                    RandomId = new Random().Next()
                });
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = "Данная группа, преподаватель или аудитория не найдены в расписании ЗабГК." +
                    "\nЯ ищу только те названия, которые полностью совпадают по каждому символу в расписании.",
                    RandomId = new Random().Next()
                });
            }
        }


        // Отписка от уведомлений
        private async Task DeleteSubscribe(long userId, CancellationToken stoppingToken)
        {
            await Task.Delay(200);
            var user = await _apiClient.GetUserByChatIdAsync(userId.ToString());
            if (user != null)
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
                        Message = "Не удалось отписать вас от рассылки.",
                        RandomId = new Random().Next()
                    });
                }
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams
                {
                    UserId = userId,
                    Message = "Вы уже не подписаны на рассылку уведомлений.",
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

        private enum MenuType
        {
            None,
            Main,
            SubscriptionManagement
        }

        private class UserDialogState
        {
            public DialogStep Step { get; set; }
            public MenuType CurrentMenu { get; set; } = MenuType.None;
        }

    }
}

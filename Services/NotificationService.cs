using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.API.DTOs;
using ZabgcScheduleBot.Parsing;
using static ZabgcScheduleBot.Parsing.FileSystem;

namespace ZabgcScheduleBot.Services
{
    public class NotificationService
    {
        private readonly ConcurrentDictionary<PlatformType, object> _botClients;
        private readonly ApiClient _apiClient;
        private readonly FileSystem _fileSystem;
        private readonly Parse _parse;
        private readonly ILogger<NotificationService> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public NotificationService(VkApi vkApi, ApiClient apiClient, FileSystem fileSystem, Parse parse, ILogger<NotificationService> logger)
        {
            _botClients = new ConcurrentDictionary<PlatformType, object>();
            _botClients[PlatformType.VK] = vkApi;
            _apiClient = apiClient;
            _fileSystem = fileSystem;
            _parse = parse;
            _logger = logger;
        }
        private static async Task<bool> UpdateAllSchedule(string currentDate)
        {
            FileSystem file = new FileSystem();
            string? currentDateFromJson = await file.GetDate("currentDate");
            return currentDateFromJson != currentDate;
        }
        private static async Task<bool> UpdatePartSchedule(string updateDate)
        {
            FileSystem file = new FileSystem();
            string? updateDateFromJson = await file.GetDate("updateDate");
            return updateDateFromJson != updateDate;
        }

        public async Task SendToUserAsync(PlatformType platform, long userId, string text)
        {
            switch (platform)
            {
                case PlatformType.VK:
                    var vk = (VkApi)_botClients[PlatformType.VK];
                    await vk.Messages.SendAsync(new MessagesSendParams
                    {
                        UserId = userId,
                        Message = text,
                        RandomId = new Random().Next()
                    });
                    break;
            }
        }

        // Метод для получения обновлений из Экспресс-расписания
        public async Task GetUpdateExpressSchedule()
        {
            if (!await _semaphore.WaitAsync(0))
            {
                _logger.LogInformation("Предыдущая проверка обновлений ещё не завершена, пропускаем");
                return;
            }
            try
            {
                string[] dates = await _parse.GetDates();
                string currentDate = dates[0];
                string updateDate = dates[1];

                bool updateAllSchedule = await UpdateAllSchedule(currentDate);
                if (updateAllSchedule)
                {
                    _logger.LogInformation("Дата расписания была обновлена.");
                    await _parse.SaveDataBeforeNotification();
                    await ProcessUpdatesAsync(isFullUpdate: true);
                }
                else
                {
                    bool updatePartSchedule = await UpdatePartSchedule(updateDate);
                    if (updatePartSchedule)
                    {
                        _logger.LogInformation("Дата составления расписания была обновлена.");
                        await ProcessUpdatesAsync(isFullUpdate: false);
                        await _parse.SaveAllPages();
                    }
                }
                await _fileSystem.RecordDates(currentDate, updateDate);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        // Общий метод перед отправкой расписания
        private async Task ProcessUpdatesAsync(bool isFullUpdate)
        {
            await _parse.SaveJsons();
            var allUsers = await _apiClient.GetAllUsersAsync();
            if (allUsers == null || !allUsers.Any())
                return;

            var groups = allUsers
                .Where(u => !string.IsNullOrEmpty(u.DescriptionName))
                .GroupBy(u => u.DescriptionName)
                .ToList();

            foreach (var group in groups)
            {
                string descriptionName = group.Key;
                var (filePath, scheduleType) = await _fileSystem.GetFullPathFromDescriptionName(descriptionName, isCurrent: true);
                var fileName = await _fileSystem.GetFileNameFromDescriptionName(descriptionName);

                if (scheduleType == ScheduleType.None)
                {
                    await NotifyAndDeleteSubscribersAsync(group, descriptionName);
                    continue;
                }

                try
                {
                    if (isFullUpdate)
                    {
                        string scheduleText = "Появилось новое расписание!\n" + await _parse.GetScheduleFromFile(filePath);
                        await SendScheduleToGroupAsync(group, scheduleText);
                    }
                    else
                    {
                        bool scheduleIsDifferent = await _parse.ScheduleIsDifferent(fileName, filePath);
                        if (scheduleIsDifferent)
                        {
                            string scheduleText = "Ваше расписание было изменено:\n" + await _parse.GetScheduleFromWeb(fileName);
                            await SendScheduleToGroupAsync(group, scheduleText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка обработки расписания для {descriptionName}");
                }
            }
        }

        // Уведомление пользователя и удаление из бд, если группа/аудитория/преподаватель больше не находится в расписании
        private async Task NotifyAndDeleteSubscribersAsync(IGrouping<string, UsersDto> users, string descriptionName)
        {
            foreach (var user in users)
            {
                await SendToUserAsync(
                    Enum.Parse<PlatformType>(user.PlatformName),
                    long.Parse(user.ChatId),
                    $"Рассылка для \"{descriptionName}\" прекращена: этот объект больше не существует в расписании.");
                await _apiClient.DeleteUserByIdAsync(user.Id);
                await Task.Delay(100);
            }
        }

        // Метод для отправки расписания по чатам
        private async Task SendScheduleToGroupAsync(IGrouping<string, UsersDto> users, string scheduleText)
        {
            foreach (var user in users)
            {
                await SendToUserAsync(
                    Enum.Parse<PlatformType>(user.PlatformName),
                    long.Parse(user.ChatId),
                    scheduleText);
                await Task.Delay(100);
            }
        }

    }

    public enum PlatformType
    {
        VK,
        MAX
    }
}

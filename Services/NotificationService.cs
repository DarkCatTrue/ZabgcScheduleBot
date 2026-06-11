using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.API;
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
        public async Task GetUpdateExpressSchedule()
        {
            if (!await _semaphore.WaitAsync(0))
            {
                _logger.LogInformation("Предыдущая проверка обновлений ещё не завершена, пропускаем");
                return;
            }
            try
            {
                string[] dates = new string[1];
                dates = await _parse.GetDates();

                string currentDate = dates[0];
                string updateDate = dates[1];

                bool updateAllSchedule = await UpdateAllSchedule(currentDate);
                if (updateAllSchedule)
                {
                    _logger.LogInformation("Дата расписания была обновлена.");
                    await SendExpressSchedule(isUpdateAllSchedule: true);
                }
                else
                {
                    bool updatePartSchedule = await UpdatePartSchedule(updateDate);
                    if (updatePartSchedule)
                    {
                        _logger.LogInformation("Дата составления расписания была обновлена.");
                        await SendExpressSchedule(isUpdateAllSchedule: false);
                    }
                }
                await _fileSystem.RecordDates(currentDate, updateDate);
            }
            finally
            {
                _semaphore.Release();
            }
            
        }

        private async Task SendExpressSchedule(bool isUpdateAllSchedule)
        {
            await _parse.SaveJsons();
            await (isUpdateAllSchedule ? SendScheduleToAllSubscribersAsync() : SendScheduleToPartSubscribersAsync() );
        }

        private async Task SendScheduleToPartSubscribersAsync()
        {
            bool isCurrent = true;
            var allUsers = await _apiClient.GetAllUsersAsync();
            if (allUsers == null || !allUsers.Any()) return;

            var groups = allUsers
                .Where(u => !string.IsNullOrEmpty(u.DescriptionName))
                .GroupBy(u => u.DescriptionName)
                .ToList();

            foreach (var group in groups)
            {
                var descriptionName = group.Key;

                var (fileName, scheduleType) = await _fileSystem.GetFileNameFromDescriptionName(descriptionName, isCurrent);
                var (filePath, scheduleTypePath) = await _fileSystem.GetFullPathFromDescriptionName(descriptionName, isCurrent);

                if (scheduleType == ScheduleType.None)
                {
                    foreach (var user in group)
                    {
                        await SendToUserAsync(
                            Enum.Parse<PlatformType>(user.PlatformName),
                            long.Parse(user.ChatId),
                            $"Рассылка для \"{descriptionName}\" прекращена: этот объект больше не существует в расписании.");

                        await _apiClient.DeleteUserByIdAsync(user.Id);
                        await Task.Delay(100);
                    }
                    continue;
                }
                try
                {
                    bool scheduleIsDifferent = await _parse.ScheduleIsDifferent(fileName, filePath);
                    if (scheduleIsDifferent)
                    {
                        string scheduleText = "Ваше расписание было изменено:\n" + await _parse.GetScheduleFromWeb(fileName);
                        foreach (var user in group)
                        {
                            await SendToUserAsync(
                                Enum.Parse<PlatformType>(user.PlatformName),
                                long.Parse(user.ChatId),
                                scheduleText);
                            await Task.Delay(100);
                        }
                    }         
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка парсинга расписания для {descriptionName}");
                }

            }
            await _parse.SaveAllPages();
        }

        private async Task SendScheduleToAllSubscribersAsync()
        {
            await _parse.SaveDataBeforeNotification();
            bool isCurrent = true;
            var allUsers = await _apiClient.GetAllUsersAsync();
            if (allUsers == null || !allUsers.Any()) return;

            var groups = allUsers
                .Where(u => !string.IsNullOrEmpty(u.DescriptionName))
                .GroupBy(u => u.DescriptionName)
                .ToList();

            foreach (var group in groups)
            {
                var descriptionName = group.Key;

                var (filepath, scheduleType) = await _fileSystem.GetFullPathFromDescriptionName(descriptionName, isCurrent);

                if (scheduleType == ScheduleType.None)
                {
                    foreach (var user in group)
                    {
                        await SendToUserAsync(
                            Enum.Parse<PlatformType>(user.PlatformName),
                            long.Parse(user.ChatId),
                            $"Рассылка для \"{descriptionName}\" прекращена: этот объект больше не существует в расписании.");

                        await _apiClient.DeleteUserByIdAsync(user.Id);
                        await Task.Delay(100);
                    }
                    continue;
                }
                try
                {
                    string scheduleText = "Появилось новое расписание!\n" + await _parse.GetScheduleFromFile(filepath);

                    foreach (var user in group)
                    {
                        await SendToUserAsync(
                            Enum.Parse<PlatformType>(user.PlatformName),
                            long.Parse(user.ChatId),
                            scheduleText);
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка парсинга расписания для {descriptionName}");
                }

            }
        }

    }

    public enum PlatformType
    {
        VK,
        MAX
    }
}

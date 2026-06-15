using System.Collections.Concurrent;
using ZabgcScheduleBot.API;
using ZabgcScheduleBot.API.DTOs;

namespace ZabgcScheduleBot.Services
{
    public class WebhookBufferService : BackgroundService
    {
        private readonly ConcurrentQueue<ExamEvent> _queue = new();
        private readonly ILogger<WebhookBufferService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
        private readonly string _staticUrl = Environment.GetEnvironmentVariable("Url_Exams");
        private Timer _timer;

        public WebhookBufferService(ILogger<WebhookBufferService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public void AddEvent(ExamEvent examEvent)
        {
            _queue.Enqueue(examEvent);
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(SendBufferedEvents, null, TimeSpan.Zero, _interval);
            return Task.CompletedTask;
        }

        private async void SendBufferedEvents(object state)
        {
            try
            {
                var events = new List<ExamEvent>();
                while (_queue.TryDequeue(out var ev))
                {
                    events.Add(ev);
                    _logger.LogInformation("Извлечено событие: {DescriptionName}", ev.DescriptionName);
                }

                _logger.LogInformation("Извлечено {Count} событий из очереди. Текущий размер очереди: {QueueCount}", events.Count, _queue.Count);

                if (events.Count == 0)
                {
                    _logger.LogInformation("Нет событий для отправки");
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var apiClient = scope.ServiceProvider.GetRequiredService<ApiClient>();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var allUsers = await apiClient.GetAllUsersAsync();

                var byDescription = events.GroupBy(e => e.DescriptionName);
                foreach (var group in byDescription)
                {
                    string description = group.Key;
                    var users = allUsers?.Where(u => u.DescriptionName == description).ToList();

                    if (users == null || !users.Any()) continue;

                    string message = $"{description}, обновилось расписание экзаменов!\nРасписание доступно по ссылке:\n{_staticUrl}";

                    foreach (var user in users)
                    {
                        await notificationService.SendToUserAsync(
                            Enum.Parse<PlatformType>(user.PlatformName),
                            long.Parse(user.ChatId),
                            message);
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в SendBufferedEvents");
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}

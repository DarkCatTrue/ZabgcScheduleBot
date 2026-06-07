using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZabgcScheduleBot.Services
{
    public class UpdateCheckerBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UpdateCheckerBackgroundService> _logger;

        public UpdateCheckerBackgroundService(IServiceProvider serviceProvider, ILogger<UpdateCheckerBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(10_000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var updateService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                        await updateService.GetUpdateExpressSchedule();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при проверке обновлений расписания");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}

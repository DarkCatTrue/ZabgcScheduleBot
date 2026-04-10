using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;

namespace ZabgcScheduleBot.Bot
{
    public class NotificationService
    {
        private readonly ConcurrentDictionary<PlatformType, object> _botClients;

        public NotificationService(VkApi vkApi)
        {
            _botClients = new ConcurrentDictionary<PlatformType, object>();
            _botClients[PlatformType.VK] = vkApi;
            //_botClients[PlatformType.MAX] = maxClient;
        }

        public async Task SendToUserAsync(PlatformType platform, long userId, string text, CancellationToken ct)
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

                //case PlatformType.MAX:
                //    var tg = (IMaxBotClient)_botClients[PlatformType.MAX];
                //    await tg.SendTextMessageAsync(userId, text);
                //    break;
            }
        }
    }

    public enum PlatformType
    {
        VK,
        MAX
    }
}

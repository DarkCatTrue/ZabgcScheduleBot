using System.Collections.Concurrent;
using VkNet;
using VkNet.Model;
using ZabgcScheduleBot.Parsing;

namespace ZabgcScheduleBot.Bot
{
    public class NotificationService
    {
        private readonly ConcurrentDictionary<PlatformType, object> _botClients;

        public NotificationService(VkApi vkApi)
        {
            _botClients = new ConcurrentDictionary<PlatformType, object>();
            _botClients[PlatformType.VK] = vkApi;
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
    }

    public enum PlatformType
    {
        VK,
        MAX
    }
}

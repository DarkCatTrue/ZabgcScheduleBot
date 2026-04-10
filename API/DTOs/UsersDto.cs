using ZabgcScheduleBot.Bot;

namespace ZabgcScheduleBot.API.DTOs
{
    public class UsersDto
    {
        public int Id { get; set; }

        public string ChatId { get; set; } = null!;

        public string DescriptionName { get; set; } = null!;

        public string PlatformName { get; set; } = null!;
    }
}

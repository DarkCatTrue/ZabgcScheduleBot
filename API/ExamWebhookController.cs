using Microsoft.AspNetCore.Mvc;
using ZabgcScheduleBot.API.DTOs;
using ZabgcScheduleBot.Services;

namespace ZabgcScheduleBot.API
{
    [ApiController]
    [Route("api/webhook")]
    public class ExamWebhookController : ControllerBase
    {
        private readonly WebhookBufferService _buffer;

        public ExamWebhookController(WebhookBufferService buffer) => _buffer = buffer;

        [HttpPost("exam")]
        public IActionResult OnExamAdded([FromBody] ExamEvent examEvent)
        {
            if (examEvent == null || string.IsNullOrEmpty(examEvent.DescriptionName))
                return BadRequest();
            _buffer.AddEvent(examEvent);
            return Ok();
        }
    }
}

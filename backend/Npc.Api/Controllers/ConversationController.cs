using System.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Npc.Api.Dtos;
using Npc.Api.Services;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("conversations")]
    public class ConversationsController(IConversationGraphService svc) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<ConversationResponse>> CreateConversation(ConversationCreateRequest req, CancellationToken ct)
        {
            return Ok(await svc.CreateConversationAsync(req.Title, ct));
        }
        [HttpPost("{conversationId:guid}/root")]
        public async Task<ActionResult<UtteranceResponse>> AddRoot(Guid conversationId, [FromBody] UtteranceCreateRequest req, CancellationToken ct)
        {
            return Ok(await svc.AddRootUtteranceAsync(conversationId, req.Text, req.CharacterId, ct));
        }

        
        [HttpPost("utterances/{fromUtteranceId:guid}/next")]
        public async Task<ActionResult<UtteranceResponse>> AddNext(Guid fromUtteranceId, UtteranceCreateRequest req, CancellationToken ct)
            => Ok(await svc.AddNextUtterance(fromUtteranceId, req.Text, req.CharacterId, ct));

        [HttpPost("branch")]
        public async Task<IActionResult> Branch(BranchCreateRequest req, CancellationToken ct)
        {
            await svc.AddBranchAsync(req.FromUtteranceId, req.ToUtteranceId, ct);
            return NoContent();
        }

        [HttpGet("{conversationId:guid}/path")]
        public async Task<ActionResult<PathResponse>> Path(Guid conversationId, CancellationToken ct)
        {
            var result = await svc.GetLinearPathAsync(conversationId, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
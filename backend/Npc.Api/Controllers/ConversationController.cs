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
        public async Task<ActionResult<UtteranceResponse>> AddNext([FromRoute] Guid fromUtteranceId, [FromBody] UtteranceCreateRequest req, CancellationToken ct)
            => Ok(await svc.AddNextUtterance(fromUtteranceId, req.Text, req.CharacterId, ct));

        [HttpPost("branch")]
        public async Task<IActionResult> Branch([FromBody] BranchCreateRequest req, CancellationToken ct)
        {
            await svc.AddBranchAsync(req.FromUtteranceId, req.ToUtteranceId, ct);
            return NoContent();
        }

        [HttpGet("{conversationId:guid}/path")]
        public async Task<ActionResult<PathResponse>> Path([FromRoute] Guid conversationId, CancellationToken ct)
        {
            var result = await svc.GetLinearPathAsync(conversationId, ct);
            return result is null ? NotFound() : Ok(result);
        }


        [HttpGet("utterances/{utteranceId:guid}")]
        public async Task<ActionResult<UtteranceDetail>> GetUtterance([FromRoute] Guid utteranceId, CancellationToken ct)
        {
            var u = await svc.GetUtteranceAsync(utteranceId, ct);
            return u is null ? NotFound() : Ok(u);
        }

        [HttpPatch("utterances/{utteranceId:guid}")]
        public async Task<ActionResult<UtteranceDetail>> UpdateUtterance(
            [FromRoute] Guid utteranceId,
            [FromBody] UtteranceUpdateRequest req,
            CancellationToken ct)
        {
            var updated = await svc.UpdateUtteranceAsync(utteranceId, req.Text, req.Tags, req.Version, ct);
            if (updated is null) return Conflict(new { error = "VersionMismatchOrDeleted" });
            return Ok(updated);
        }

        [HttpDelete("utterances/{utteranceId:guid}")]
        public async Task<IActionResult> SoftDelete([FromRoute] Guid utteranceId, CancellationToken ct)
        {
            var ok = await svc.SoftDeleteUtteranceAsync(utteranceId, ct);
            return ok ? NoContent() : NotFound();
        }
        
        [HttpGet("{conversationId:guid}/graph")]
        public async Task<ActionResult<GraphResponse>> GetGraph(
            [FromRoute] Guid conversationId,
            [FromQuery] int depth = 10,
            CancellationToken ct = default)
        {
            var graph = await svc.GetGraphAsync(conversationId, depth, ct);
            return graph is null ? NotFound() : Ok(graph);
        }
    }
}
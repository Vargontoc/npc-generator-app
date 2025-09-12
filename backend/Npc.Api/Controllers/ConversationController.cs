using Microsoft.AspNetCore.Mvc;
using Npc.Api.Dtos;
using Npc.Api.Services;
using Npc.Api.Services.Impl;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("conversations")]
    public class ConversationsController(IConversationGraphService svc, IAgentConversationService agent, ITtsService tts) : ControllerBase
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
            if (req.Weight is not null)
                await svc.SetBranchWeightAsync(req.FromUtteranceId, req.ToUtteranceId, req.Weight.Value, ct);
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



        [HttpPost("{conversationId:guid}/random-path")]
        public async Task<ActionResult<PathResponse>> RandomPath([FromRoute] Guid conversationId, [FromQuery] int maxDepth = 15, CancellationToken ct = default)
        {
            var path = await svc.GetRandomPathAsync(conversationId, maxDepth, ct);
            return path is null ? NotFound() : Ok(path);
        }

        [HttpPost("branch/weight")]
        public async Task<IActionResult> SetBranchWeight([FromQuery] Guid fromId, [FromQuery] Guid toId, [FromQuery] double weight, CancellationToken ct = default)
        {
            await svc.SetBranchWeightAsync(fromId, toId, weight, ct);
            return NoContent();
        }

        [HttpPost("import")]
        public async Task<ActionResult<ConversationResponse>> Import(
            [FromBody] ConversationImportRequest req,
            CancellationToken ct)
        {
            var res = await svc.ImportConversationAsync(req, ct);
            return Ok(res);
        }

        [HttpPost("{conversationId:guid}/auto-expand")]
        public async Task<ActionResult<object>> AutoExpand(
            [FromRoute] Guid conversationId,
            [FromBody] AutoExpandedRequest req,
            CancellationToken ct)
        {
            var original = req.Count;
            var clamped = original < 1 ? 1 : (original > 20 ? 20 : original);
            if (clamped != original)
                Response.Headers.TryAdd("X-Count-Adjusted", clamped.ToString());

            req = req with { Count = clamped };

            var list = await svc.AutoExpandedAsync(conversationId, req, ct);
            return Ok(new { generated = list.Length, items = list });
        }

        [HttpGet("{conversationId:guid}/export")]
        public async Task<ActionResult<ConversationExportResponse>> Export(
            Guid conversationId,
            [FromQuery] int depth = 25,
            CancellationToken ct = default)
        {
            var data = await svc.ExportConversationAsync(conversationId, depth, ct);
            return data is null ? NotFound() : Ok(data);
        }
        
        [HttpGet("voices")]
        public async Task<IActionResult> Voices(CancellationToken ct)
        {
            try
            {
                var json = await tts.GetVoices(ct);
                return Content(json, "application/json");
            }
            catch (TtsException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }

        [HttpPost("{conversationId:guid}/suggest")]
        public async Task<IActionResult> SuggestBranch(
            Guid conversationId,
            [FromBody] AutoExpandedRequest req,
            CancellationToken ct)
        {
            var safeCount = req.Count is < 1 or > 10 ? 3 : req.Count;
            var adjusted = new AutoExpandedRequest(conversationId, safeCount, req.Context, req.FromUtteranceId);

            var items = await agent.GenerateAsync(conversationId, adjusted, ct);
            return Ok(new
            {
                conversationId,
                count = items.Length,
                items
            });
        }

        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] TtsSynthesizeRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Text) || string.IsNullOrWhiteSpace(req.Voice))
                return BadRequest(new { error = "Text and Voice are required." });

            try
            {
                var (stream, contentType, fileName) = await tts.GenerateVoice(req, ct);
                return File(stream, contentType, fileName ?? $"{req.Voice}.{req.Format}");
            }
            catch (TtsException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }
    }
}
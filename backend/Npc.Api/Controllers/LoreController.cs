using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;
using Npc.Api.Application.Mediator;
using Npc.Api.Application.Commands;
using Npc.Api.Application.Queries;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("lores")]
    public class LoreController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoreResponse>>> List([FromQuery] Guid? worldId, CancellationToken ct)
        {
            var query = new GetLoreByWorldIdQuery(worldId);
            var lores = await mediator.SendAsync(query, ct);

            var list = lores.Select(l => new LoreResponse(l.Id, l.Title, l.Text, l.WorldId, l.CreatedAt, l.UpdatedAt));
            return Ok(list);
        }

        // GET /lore/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<LoreResponse>> Get([FromRoute] Guid id, CancellationToken ct)
        {
            var query = new GetLoreByIdQuery(id);
            var l = await mediator.SendAsync(query, ct);
            if (l is null) return NotFound();
            return Ok(new LoreResponse(l.Id, l.Title, l.Text, l.WorldId, l.CreatedAt, l.UpdatedAt));
        }

        // POST /lore
        [HttpPost]
        public async Task<ActionResult<LoreResponse>> Create([FromBody] LoreRequest req, CancellationToken ct)
        {
            try
            {
                var command = new CreateLoreCommand(req.Title, req.Text, req.WorldId);
                var entity = await mediator.SendAsync(command, ct);
                return CreatedAtAction(nameof(Get), new { id = entity.Id },
                    new LoreResponse(entity.Id, entity.Title, entity.Text, entity.WorldId, entity.CreatedAt, entity.UpdatedAt));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("World"))
            {
                return BadRequest(new { error = "WorldNotFound" });
            }
        }

        // PUT /lore/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<LoreResponse>> Update(Guid id, LoreRequest req, CancellationToken ct)
        {
            try
            {
                var command = new UpdateLoreCommand(id, req.Title, req.Text, req.WorldId);
                var entity = await mediator.SendAsync(command, ct);
                return Ok(new LoreResponse(entity.Id, entity.Title, entity.Text, entity.WorldId, entity.CreatedAt, entity.UpdatedAt));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Lore"))
            {
                return NotFound();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("World"))
            {
                return BadRequest(new { error = "WorldNotFound" });
            }
        }

        // DELETE /lore/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                var command = new DeleteLoreCommand(id);
                await mediator.SendAsync(command, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpPost("suggest")]
        public async Task<ActionResult<LoreSuggestResponse>> Suggest(
            [FromBody] LoreSuggestRequest req,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return BadRequest(new { error = "PromptRequired" });

            var count = req.Count is < 1 or > 10 ? 1 : req.Count;
            var command = new SuggestLoreCommand(req.WorldId, req.Prompt, count, req.DryRun);
            var response = await mediator.SendAsync(command, ct);

            return Ok(response);
        }
    }
}
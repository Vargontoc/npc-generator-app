using AutoMapper;
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
    public class LoreController(IMediator mediator, IMapper mapper) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoreResponse>>> List([FromQuery] Guid? worldId, CancellationToken ct)
        {
            var query = new GetLoreByWorldIdQuery(worldId);
            var lores = await mediator.SendAsync(query, ct);

            var list = mapper.Map<IEnumerable<LoreResponse>>(lores);
            return Ok(list);
        }

        // GET /lore/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<LoreResponse>> Get([FromRoute] Guid id, CancellationToken ct)
        {
            var query = new GetLoreByIdQuery(id);
            var l = await mediator.SendAsync(query, ct);
            if (l is null) return NotFound();
            var response = mapper.Map<LoreResponse>(l);
            return Ok(response);
        }

        // POST /lore
        [HttpPost]
        public async Task<ActionResult<LoreResponse>> Create([FromBody] LoreRequest req, CancellationToken ct)
        {
            try
            {
                var command = new CreateLoreCommand(req);
                var entity = await mediator.SendAsync(command, ct);
                var response = mapper.Map<LoreResponse>(entity);
                return CreatedAtAction(nameof(Get), new { id = entity.Id }, response);
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
                var command = new UpdateLoreCommand(id, req);
                var entity = await mediator.SendAsync(command, ct);
                var response = mapper.Map<LoreResponse>(entity);
                return Ok(response);
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
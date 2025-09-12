using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("lores")]
    public class LoreController(CharacterDbContext ctx, IAgentConversationService svc) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoreResponse>>> List([FromQuery] Guid? worldId, CancellationToken ct)
        {
            var query = ctx.Set<Lore>().AsNoTracking().AsQueryable();
            if (worldId is not null)
                query = query.Where(l => l.WorldId == worldId);

            var list = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LoreResponse(l.Id, l.Title, l.Text, l.WorldId, l.CreatedAt, l.UpdatedAt))
                .ToListAsync(ct);

            return Ok(list);
        }

        // GET /lore/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<LoreResponse>> Get([FromRoute] Guid id, CancellationToken ct)
        {
            var l = await ctx.Set<Lore>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (l is null) return NotFound();
            return Ok(new LoreResponse(l.Id, l.Title, l.Text, l.WorldId, l.CreatedAt, l.UpdatedAt));
        }

        // POST /lore
        [HttpPost]
        public async Task<ActionResult<LoreResponse>> Create([FromBody] LoreRequest req, CancellationToken ct)
        {
            if (req.WorldId is not null)
            {
                var exists = await ctx.Set<World>().AnyAsync(w => w.Id == req.WorldId, ct);
                if (!exists) return BadRequest(new { error = "WorldNotFound" });
            }

            var entity = new Lore
            {
                Title = req.Title,
                Text = req.Text,
                WorldId = req.WorldId
            };

            ctx.Add(entity);
            await ctx.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(Get), new { id = entity.Id },
                new LoreResponse(entity.Id, entity.Title, entity.Text, entity.WorldId, entity.CreatedAt, entity.UpdatedAt));
        }

        // PUT /lore/{id}
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<LoreResponse>> Update(Guid id, LoreRequest req, CancellationToken ct)
        {
            var entity = await ctx.Set<Lore>().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound();

            if (req.WorldId is not null)
            {
                var exists = await ctx.Set<World>().AnyAsync(w => w.Id == req.WorldId, ct);
                if (!exists) return BadRequest(new { error = "WorldNotFound" });
            }

            entity.Title = req.Title;
            entity.Text = req.Text;
            entity.WorldId = req.WorldId;

            await ctx.SaveChangesAsync(ct);
            return Ok(new LoreResponse(entity.Id, entity.Title, entity.Text, entity.WorldId, entity.CreatedAt, entity.UpdatedAt));
        }

        // DELETE /lore/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var entity = await ctx.Set<Lore>().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound();

            ctx.Remove(entity);
            await ctx.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpPost("suggest")]
        public async Task<ActionResult<LoreSuggestResponse>> Suggest(
            [FromBody] LoreSuggestRequest req,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return BadRequest(new { error = "PromptRequired" });

            var count = req.Count is < 1 or > 10 ? 1 : req.Count;
            var adjusted = req with { Count = count };

            var items = await svc.GenerateLoreAsync(adjusted, ct);
            if (items.Length == 0)
                return Ok(new LoreSuggestResponse(false, Array.Empty<LoreSuggestedItem>()));

            if (req.DryRun) // no persist
                return Ok(new LoreSuggestResponse(false, items));

            var now = DateTimeOffset.UtcNow;
            foreach (var it in items)
            {
                var entity = new Lore
                {
                    Title = it.Title,
                    Text = it.Text,
                    WorldId = req.WorldId,
                    IsGenerated = true,
                    GenerationSource = "agent",
                    GenerationMeta = it.Model is null ? null : $"{{\"model\":\"{it.Model}\"}}",
                    GeneratedAt = now
                };
                ctx.Add(entity);
            }
            await ctx.SaveChangesAsync(ct);

            return Ok(new LoreSuggestResponse(true, items));
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;
using Npc.Api.Infrastructure.Audit;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class CharacterController(CharacterDbContext ctx, IModerationService mod, IAuditService audit) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Character>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var items = await ctx.Set<Character>().AsNoTracking().OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return Ok(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Character>> GetCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await ctx.Set<Character>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return entity is null ? NotFound() : Ok(entity);
        }

        [HttpPost]
        public async Task<ActionResult<Character>> CreateCharacter([FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            var advisory = await mod.AnalyzeAsync(req.Age, req.Description, ct);
            if (advisory.HasAdvisory)
                Response.Headers.Append("X-Moderation-Warnings", string.Join(",", advisory.Flags));

            var entity = new Character
            {
                Name = req.Name,
                Age = req.Age,
                Description = req.Description,
                AvatarUrl = req.AvatarUrl
            };

            ctx.Add(entity);
            Infrastructure.Observability.Telemetry.CharactersCreated.Add(1);
            await ctx.SaveChangesAsync(ct);

            // Audit trail
            await audit.LogCharacterChangeAsync("CREATE", entity.Id, null, entity, "api-user", ct);

            return CreatedAtAction(nameof(GetCharacter), new { id = entity.Id }, entity);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Character>> UpdateCharacter([FromRoute] Guid id, [FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            var advisory = await mod.AnalyzeAsync(req.Age, req.Description, ct);
            if (advisory.HasAdvisory)
                Response.Headers.Append("X-Moderation-Warnings", string.Join(",", advisory.Flags));

            var entity = await ctx.Set<Character>().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity is null) return NotFound();

            // Capture old values for audit
            var oldEntity = new { entity.Name, entity.Age, entity.Description, entity.AvatarUrl };

            entity.Name = req.Name;
            entity.Age = req.Age;
            entity.Description = req.Description;
            entity.AvatarUrl = req.AvatarUrl;

            await ctx.SaveChangesAsync(ct);

            // Audit trail
            await audit.LogCharacterChangeAsync("UPDATE", entity.Id, oldEntity, entity, "api-user", ct);

            return Ok(entity);
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await ctx.Set<Character>().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity is null) return NotFound();

            // Capture entity for audit before deletion
            var deletedEntity = new { entity.Id, entity.Name, entity.Age, entity.Description, entity.AvatarUrl };

            ctx.Remove(entity);
            await ctx.SaveChangesAsync(ct);

            // Audit trail
            await audit.LogCharacterChangeAsync("DELETE", id, deletedEntity, null, "api-user", ct);

            return NoContent();
        }

    }
}
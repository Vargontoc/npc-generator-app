using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class CharacterController(CharacterDbContext ctx, IModerationService mod) : ControllerBase
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
        public async Task<ActionResult<Character>> CreateCharacter([FromBody] CharacterCreateRequest req, CancellationToken ct = default)
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
            await ctx.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetCharacter), new { id = entity.Id }, entity);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Character>> UpdateCharacter([FromRoute] Guid id, [FromBody] CharacterUpdateRequest req, CancellationToken ct = default)
        {
            var advisory = await mod.AnalyzeAsync(req.Age, req.Description, ct);
            if (advisory.HasAdvisory)
                Response.Headers.Append("X-Moderation-Warnings", string.Join(",", advisory.Flags));

            var entity = await ctx.Set<Character>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity is null) return NotFound();
            entity.Name = req.Name;
            entity.Age = req.Age;
            entity.Description = req.Description;
            entity.AvatarUrl = req.AvatarUrl;

            await ctx.SaveChangesAsync(ct);
            return Ok(entity);
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await ctx.Set<Character>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity is null) return NotFound();
            ctx.Remove(entity);
            await ctx.SaveChangesAsync(ct);
            return NoContent();
        }

    }
}
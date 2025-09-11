using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("worlds")]
    public class WorldController(CharacterDbContext ctx) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorldResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var list = await ctx.Set<World>()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new WorldResponse(w.Id, w.Name, w.Description, w.CreatedAt, w.UpdatedAt)).ToListAsync(ct);

            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorldResponse>> GetWorld([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await ctx.Set<World>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound();
            return Ok(new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
        }

        [HttpPost]
        public async Task<ActionResult<WorldResponse>> CreateWorld([FromBody] WorldRequest request, CancellationToken ct = default)
        {
            var entity = new World { Name = request.Name, Description = request.Description };
            ctx.Add(entity);

            await ctx.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetWorld), new { id = entity.Id },
             new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<WorldResponse>> UpdateWorld([FromRoute] Guid id, [FromBody] WorldRequest request, CancellationToken ct = default)
        {
            var entity = await ctx.Set<World>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return NotFound();
            entity.Name = request.Name;
            entity.Description = request.Description;

            await ctx.SaveChangesAsync(ct);
            return Ok(new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
        }
        
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await ctx.Set<World>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

            if (entity is null) return NotFound();
            ctx.Remove(entity);
            await ctx.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}


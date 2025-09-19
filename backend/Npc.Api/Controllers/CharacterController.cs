using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Repositories;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class CharacterController(ICharacterRepository repository, IModerationService mod, IAuditService audit) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Character>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var (items, totalCount) = await repository.GetPagedAsync(page, pageSize, ct);
            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            return Ok(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Character>> GetCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await repository.GetByIdAsync(id, ct);
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

            var createdEntity = await repository.AddAsync(entity, ct);
            Infrastructure.Observability.Telemetry.CharactersCreated.Add(1);

            // Audit trail
            await audit.LogCharacterChangeAsync("CREATE", createdEntity.Id, null, createdEntity, "api-user", ct);

            return CreatedAtAction(nameof(GetCharacter), new { id = createdEntity.Id }, createdEntity);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Character>> UpdateCharacter([FromRoute] Guid id, [FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            var advisory = await mod.AnalyzeAsync(req.Age, req.Description, ct);
            if (advisory.HasAdvisory)
                Response.Headers.Append("X-Moderation-Warnings", string.Join(",", advisory.Flags));

            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null) return NotFound();

            // Capture old values for audit
            var oldEntity = new { entity.Name, entity.Age, entity.Description, entity.AvatarUrl };

            entity.Name = req.Name;
            entity.Age = req.Age;
            entity.Description = req.Description;
            entity.AvatarUrl = req.AvatarUrl;

            var updatedEntity = await repository.UpdateAsync(entity, ct);

            // Audit trail
            await audit.LogCharacterChangeAsync("UPDATE", updatedEntity.Id, oldEntity, updatedEntity, "api-user", ct);

            return Ok(updatedEntity);
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null) return NotFound();

            // Capture entity for audit before deletion
            var deletedEntity = new { entity.Id, entity.Name, entity.Age, entity.Description, entity.AvatarUrl };

            await repository.DeleteAsync(id, ct);

            // Audit trail
            await audit.LogCharacterChangeAsync("DELETE", id, deletedEntity, null, "api-user", ct);

            return NoContent();
        }

    }
}
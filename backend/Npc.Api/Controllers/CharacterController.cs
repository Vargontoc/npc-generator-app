using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;
using Npc.Api.Infrastructure.Audit;
using Npc.Api.Repositories;
using Npc.Api.Application.Mediator;
using Npc.Api.Application.Commands;
using Npc.Api.Application.Queries;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class CharacterController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Character>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var query = new GetCharactersPagedQuery(page, pageSize);
            var (items, totalCount) = await mediator.SendAsync(query, ct);
            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            return Ok(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Character>> GetCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var query = new GetCharacterByIdQuery(id);
            var entity = await mediator.SendAsync(query, ct);
            return entity is null ? NotFound() : Ok(entity);
        }

        [HttpPost]
        public async Task<ActionResult<Character>> CreateCharacter([FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            var command = new CreateCharacterCommand(req.Name, req.Age, req.Description, req.AvatarUrl);
            var createdEntity = await mediator.SendAsync(command, ct);

            return CreatedAtAction(nameof(GetCharacter), new { id = createdEntity.Id }, createdEntity);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Character>> UpdateCharacter([FromRoute] Guid id, [FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            try
            {
                var command = new UpdateCharacterCommand(id, req.Name, req.Age, req.Description, req.AvatarUrl);
                var updatedEntity = await mediator.SendAsync(command, ct);
                return Ok(updatedEntity);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            try
            {
                var command = new DeleteCharacterCommand(id);
                await mediator.SendAsync(command, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

    }
}
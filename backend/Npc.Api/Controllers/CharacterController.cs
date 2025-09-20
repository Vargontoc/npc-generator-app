using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Npc.Api.Dtos;
using Npc.Api.Application.Mediator;
using Npc.Api.Application.Commands;
using Npc.Api.Application.Queries;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class CharacterController(IMediator mediator, IMapper mapper) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CharacterResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var query = new GetCharactersPagedQuery(page, pageSize);
            var (items, totalCount) = await mediator.SendAsync(query, ct);
            Response.Headers.Append("X-Total-Count", totalCount.ToString());
            var response = mapper.Map<IEnumerable<CharacterResponse>>(items);
            return Ok(response);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CharacterResponse>> GetCharacter([FromRoute] Guid id, CancellationToken ct = default)
        {
            var query = new GetCharacterByIdQuery(id);
            var entity = await mediator.SendAsync(query, ct);
            if (entity is null) return NotFound();
            var response = mapper.Map<CharacterResponse>(entity);
            return Ok(response);
        }

        [HttpPost]
        public async Task<ActionResult<CharacterResponse>> CreateCharacter([FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            var command = new CreateCharacterCommand(req);
            var createdEntity = await mediator.SendAsync(command, ct);
            var response = mapper.Map<CharacterResponse>(createdEntity);

            return CreatedAtAction(nameof(GetCharacter), new { id = createdEntity.Id }, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CharacterResponse>> UpdateCharacter([FromRoute] Guid id, [FromBody] CharacterRequest req, CancellationToken ct = default)
        {
            try
            {
                var command = new UpdateCharacterCommand(id, req);
                var updatedEntity = await mediator.SendAsync(command, ct);
                var response = mapper.Map<CharacterResponse>(updatedEntity);
                return Ok(response);
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
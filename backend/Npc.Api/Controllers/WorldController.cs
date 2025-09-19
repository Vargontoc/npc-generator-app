using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Application.Mediator;
using Npc.Api.Application.Commands;
using Npc.Api.Application.Queries;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("worlds")]
    public class WorldController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorldResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var query = new GetWorldsPagedQuery(page, pageSize);
            var (items, totalCount) = await mediator.SendAsync(query, ct);

            var list = items.Select(w => new WorldResponse(w.Id, w.Name, w.Description, w.CreatedAt, w.UpdatedAt));
            Response.Headers.Append("X-Total-Count", totalCount.ToString());

            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorldResponse>> GetWorld([FromRoute] Guid id, CancellationToken ct = default)
        {
            var query = new GetWorldByIdQuery(id);
            var entity = await mediator.SendAsync(query, ct);
            if (entity is null) return NotFound();
            return Ok(new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
        }

        [HttpPost]
        public async Task<ActionResult<WorldResponse>> CreateWorld([FromBody] WorldRequest request, CancellationToken ct = default)
        {
            var command = new CreateWorldCommand(request.Name, request.Description);
            var entity = await mediator.SendAsync(command, ct);

            return CreatedAtAction(nameof(GetWorld), new { id = entity.Id },
             new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<WorldResponse>> UpdateWorld([FromRoute] Guid id, [FromBody] WorldRequest request, CancellationToken ct = default)
        {
            try
            {
                var command = new UpdateWorldCommand(id, request.Name, request.Description);
                var entity = await mediator.SendAsync(command, ct);
                return Ok(new WorldResponse(entity.Id, entity.Name, entity.Description, entity.CreatedAt, entity.UpdatedAt));
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }
        
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteWorld([FromRoute] Guid id, CancellationToken ct = default)
        {
            try
            {
                var command = new DeleteWorldCommand(id);
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


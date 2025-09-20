using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Npc.Api.Application.Mediator;
using Npc.Api.Application.Commands;
using Npc.Api.Dtos;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters/bulk")]
    public class BulkCharacterController(IMediator mediator, IMapper mapper) : ControllerBase
    {
        /// <summary>
        /// Bulk create multiple characters
        /// </summary>
        /// <param name="requests">Array of character creation requests</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Bulk operation result with successful and failed items</returns>
        [HttpPost]
        public async Task<ActionResult<BulkCharacterResponse>> BulkCreateCharacters(
            [FromBody] CharacterRequest[] requests,
            CancellationToken ct = default)
        {
            if (requests.Length == 0)
                return BadRequest(new { error = "At least one character request is required" });

            if (requests.Length > 100)
                return BadRequest(new { error = "Maximum 100 characters can be created in a single bulk operation" });

            var command = new BulkCreateCharactersCommand(requests);
            var result = await mediator.SendAsync(command, ct);

            var response = new BulkCharacterResponse
            {
                SuccessfulItems = mapper.Map<IEnumerable<CharacterResponse>>(result.SuccessfulItems),
                Errors = result.Errors.Select(e => new BulkOperationErrorResponse
                {
                    ItemIdentifier = e.ItemIdentifier,
                    ErrorType = e.ErrorType,
                    ErrorMessage = e.ErrorMessage
                }),
                TotalProcessed = result.TotalProcessed,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount,
                ProcessingTimeMs = (int)result.ProcessingTime.TotalMilliseconds
            };

            // Return 207 Multi-Status if there were both successes and errors
            if (result.SuccessCount > 0 && result.ErrorCount > 0)
                return StatusCode(207, response);

            // Return 200 if all succeeded
            if (result.ErrorCount == 0)
                return Ok(response);

            // Return 400 if all failed
            return BadRequest(response);
        }

        /// <summary>
        /// Bulk update multiple characters
        /// </summary>
        /// <param name="requests">Array of character update requests with IDs</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Bulk operation result with successful and failed items</returns>
        [HttpPut]
        public async Task<ActionResult<BulkCharacterResponse>> BulkUpdateCharacters(
            [FromBody] BulkUpdateCharacterRequest[] requests,
            CancellationToken ct = default)
        {
            if (requests.Length == 0)
                return BadRequest(new { error = "At least one character update request is required" });

            if (requests.Length > 100)
                return BadRequest(new { error = "Maximum 100 characters can be updated in a single bulk operation" });

            var characterUpdates = requests.Select(r => (r.Id, r.Character)).ToArray();
            var command = new BulkUpdateCharactersCommand(characterUpdates);
            var result = await mediator.SendAsync(command, ct);

            var response = new BulkCharacterResponse
            {
                SuccessfulItems = mapper.Map<IEnumerable<CharacterResponse>>(result.SuccessfulItems),
                Errors = result.Errors.Select(e => new BulkOperationErrorResponse
                {
                    ItemIdentifier = e.ItemIdentifier,
                    ErrorType = e.ErrorType,
                    ErrorMessage = e.ErrorMessage
                }),
                TotalProcessed = result.TotalProcessed,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount,
                ProcessingTimeMs = (int)result.ProcessingTime.TotalMilliseconds
            };

            // Return 207 Multi-Status if there were both successes and errors
            if (result.SuccessCount > 0 && result.ErrorCount > 0)
                return StatusCode(207, response);

            // Return 200 if all succeeded
            if (result.ErrorCount == 0)
                return Ok(response);

            // Return 400 if all failed
            return BadRequest(response);
        }

        /// <summary>
        /// Bulk delete multiple characters
        /// </summary>
        /// <param name="request">Request containing array of character IDs to delete</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Bulk operation result with deletion status</returns>
        [HttpDelete]
        public async Task<ActionResult<BulkDeleteResponse>> BulkDeleteCharacters(
            [FromBody] BulkDeleteRequest request,
            CancellationToken ct = default)
        {
            if (request.Ids.Length == 0)
                return BadRequest(new { error = "At least one character ID is required" });

            if (request.Ids.Length > 100)
                return BadRequest(new { error = "Maximum 100 characters can be deleted in a single bulk operation" });

            var command = new BulkDeleteCharactersCommand(request.Ids);
            var result = await mediator.SendAsync(command, ct);

            var response = new BulkDeleteResponse
            {
                Errors = result.Errors.Select(e => new BulkOperationErrorResponse
                {
                    ItemIdentifier = e.ItemIdentifier,
                    ErrorType = e.ErrorType,
                    ErrorMessage = e.ErrorMessage
                }),
                TotalProcessed = result.TotalProcessed,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount,
                ProcessingTimeMs = (int)result.ProcessingTime.TotalMilliseconds
            };

            // Return 207 Multi-Status if there were both successes and errors
            if (result.SuccessCount > 0 && result.ErrorCount > 0)
                return StatusCode(207, response);

            // Return 200 if all succeeded
            if (result.ErrorCount == 0)
                return Ok(response);

            // Return 400 if all failed
            return BadRequest(response);
        }

        /// <summary>
        /// Bulk delete characters by age range
        /// </summary>
        /// <param name="minAge">Minimum age (inclusive)</param>
        /// <param name="maxAge">Maximum age (inclusive)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Number of characters deleted</returns>
        [HttpDelete("by-age")]
        public ActionResult<BulkDeleteByConditionResponse> BulkDeleteCharactersByAge(
            [FromQuery] int minAge,
            [FromQuery] int maxAge,
            CancellationToken ct = default)
        {
            if (minAge < 0 || maxAge < 0 || minAge > maxAge)
                return BadRequest(new { error = "Invalid age range" });

            // For safety, require confirmation for bulk deletes by condition
            if (!Request.Headers.ContainsKey("X-Confirm-Bulk-Delete"))
                return BadRequest(new { error = "Bulk delete by condition requires X-Confirm-Bulk-Delete header" });

            var startTime = DateTime.UtcNow;

            // This would be implemented as a separate command/handler for safety
            // For now, return not implemented
            return StatusCode(501, new { error = "Bulk delete by condition not yet implemented for safety reasons" });
        }
    }

    // DTOs for Bulk Operations
    public class BulkUpdateCharacterRequest
    {
        public Guid Id { get; set; }
        public CharacterRequest Character { get; set; } = null!;
    }

    public class BulkDeleteRequest
    {
        public Guid[] Ids { get; set; } = Array.Empty<Guid>();
    }

    public class BulkCharacterResponse
    {
        public IEnumerable<CharacterResponse> SuccessfulItems { get; set; } = Enumerable.Empty<CharacterResponse>();
        public IEnumerable<BulkOperationErrorResponse> Errors { get; set; } = Enumerable.Empty<BulkOperationErrorResponse>();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int ProcessingTimeMs { get; set; }
    }

    public class BulkDeleteResponse
    {
        public IEnumerable<BulkOperationErrorResponse> Errors { get; set; } = Enumerable.Empty<BulkOperationErrorResponse>();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int ProcessingTimeMs { get; set; }
    }

    public class BulkDeleteByConditionResponse
    {
        public int DeletedCount { get; set; }
        public int ProcessingTimeMs { get; set; }
        public string Condition { get; set; } = string.Empty;
    }

    public class BulkOperationErrorResponse
    {
        public string ItemIdentifier { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
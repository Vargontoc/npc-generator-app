using Microsoft.AspNetCore.Mvc;
using Npc.Api.Infrastructure.BackgroundJobs;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IBackgroundJobService _backgroundJobService;
        private readonly ILogger<JobsController> _logger;

        public JobsController(IBackgroundJobService backgroundJobService, ILogger<JobsController> logger)
        {
            _backgroundJobService = backgroundJobService;
            _logger = logger;
        }

        /// <summary>
        /// Enqueue image generation for a character
        /// </summary>
        [HttpPost("characters/{characterId}/generate-image")]
        public ActionResult<JobResponse> EnqueueImageGeneration(
            Guid characterId,
            [FromBody] ImageGenerationJobRequest request)
        {
            try
            {
                var jobId = _backgroundJobService.EnqueueImageGeneration(characterId, request.Prompt);
                return Ok(new JobResponse { JobId = jobId, Status = "Enqueued" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue image generation for character {CharacterId}", characterId);
                return StatusCode(500, "Failed to enqueue job");
            }
        }

        /// <summary>
        /// Enqueue bulk character generation for a world
        /// </summary>
        [HttpPost("worlds/{worldId}/bulk-generate-characters")]
        public ActionResult<JobResponse> EnqueueBulkCharacterGeneration(
            Guid worldId,
            [FromBody] BulkGenerationJobRequest request)
        {
            try
            {
                if (request.Count <= 0 || request.Count > 100)
                {
                    return BadRequest("Count must be between 1 and 100");
                }

                var jobId = _backgroundJobService.EnqueueBulkCharacterGeneration(worldId, request.Count);
                return Ok(new JobResponse { JobId = jobId, Status = "Enqueued" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue bulk character generation for world {WorldId}", worldId);
                return StatusCode(500, "Failed to enqueue job");
            }
        }

        /// <summary>
        /// Enqueue lore generation for a world
        /// </summary>
        [HttpPost("worlds/{worldId}/generate-lore")]
        public ActionResult<JobResponse> EnqueueLoreGeneration(
            Guid worldId,
            [FromBody] LoreGenerationJobRequest request)
        {
            try
            {
                var jobId = _backgroundJobService.EnqueueLoreGeneration(worldId, request.Theme);
                return Ok(new JobResponse { JobId = jobId, Status = "Enqueued" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue lore generation for world {WorldId}", worldId);
                return StatusCode(500, "Failed to enqueue job");
            }
        }

        /// <summary>
        /// Enqueue conversation cleanup job
        /// </summary>
        [HttpPost("cleanup/conversations")]
        public ActionResult<JobResponse> EnqueueConversationCleanup()
        {
            try
            {
                var jobId = _backgroundJobService.EnqueueConversationCleanup();
                return Ok(new JobResponse { JobId = jobId, Status = "Enqueued" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue conversation cleanup");
                return StatusCode(500, "Failed to enqueue job");
            }
        }

        /// <summary>
        /// Schedule world statistics update
        /// </summary>
        [HttpPost("worlds/{worldId}/schedule-statistics-update")]
        public ActionResult<JobResponse> ScheduleWorldStatisticsUpdate(
            Guid worldId,
            [FromBody] ScheduleJobRequest request)
        {
            try
            {
                var delay = TimeSpan.FromMinutes(request.DelayMinutes);
                var jobId = _backgroundJobService.ScheduleWorldStatisticsUpdate(worldId, delay);
                return Ok(new JobResponse { JobId = jobId, Status = "Scheduled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule statistics update for world {WorldId}", worldId);
                return StatusCode(500, "Failed to schedule job");
            }
        }

        /// <summary>
        /// Delete a background job
        /// </summary>
        [HttpDelete("{jobId}")]
        public ActionResult DeleteJob(string jobId)
        {
            try
            {
                _backgroundJobService.DeleteJob(jobId);
                return Ok(new { Message = "Job deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete job {JobId}", jobId);
                return StatusCode(500, "Failed to delete job");
            }
        }
    }

    // DTOs for job requests
    public class ImageGenerationJobRequest
    {
        public required string Prompt { get; set; }
    }

    public class BulkGenerationJobRequest
    {
        public required int Count { get; set; }
    }

    public class LoreGenerationJobRequest
    {
        public required string Theme { get; set; }
    }

    public class ScheduleJobRequest
    {
        public required int DelayMinutes { get; set; }
    }

    public class JobResponse
    {
        public required string JobId { get; set; }
        public required string Status { get; set; }
    }
}
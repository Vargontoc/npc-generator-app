using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npc.Api.Data;
using Npc.Api.Dtos;
using Npc.Api.Entities;
using Npc.Api.Services;
using static Npc.Api.Services.Impl.ImageGenService;

namespace Npc.Api.Controllers
{
    [ApiController]
    [Route("characters")]
    public class ImagesController(CharacterDbContext ctx, IImageGenService svc) : ControllerBase
    {

        [HttpGet("models")]
        public async Task<IActionResult> Models(CancellationToken ct)
        {
            try
            {
                var json = await svc.GetModelsRawAsync(ct);
                return Content(json, "application/json");
            }
            catch (ImageGenException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }


        [HttpPost("generate")]
        public async Task<ActionResult<ImageJobAccepted>> Generate([FromBody] ImageRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return BadRequest(new { error = "Prompt required" });

            try
            {
                var accepted = await svc.GenerateAsync(req, ct);
                return Accepted(new { accepted.Job_Id, accepted.Status });
            }
            catch (ImageGenException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }

        [HttpGet("jobs/{jobId}")]
        public async Task<ActionResult<ImageJobStatus>> Job([FromRoute] string jobId, CancellationToken ct)
        {
            try
            {
                var status = await svc.GetJobAsync(jobId, ct);
                return Ok(status);
            }
            catch (ImageGenException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }
        
            // Assign first image of completed job as character avatar
        [HttpPost("characters/{characterId:guid}/assign-avatar")]
        public async Task<IActionResult> AssignAvatar(Guid characterId, AssignAvatarRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.JobId))
                return BadRequest(new { error = "JobId required" });

            var character = await ctx.Set<Character>().FirstOrDefaultAsync(c => c.Id == characterId, ct);
            if (character is null) return NotFound(new { error = "CharacterNotFound" });

            try
            {
                var status = await svc.GetJobAsync(req.JobId, ct);
                if (!string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = "JobNotCompleted", status.Status });

                var first = status.Images.FirstOrDefault();
                if (first is null)
                    return BadRequest(new { error = "NoImagesInJob" });

                character.AvatarUrl = first.Url;
                await ctx.SaveChangesAsync(ct);

                return Ok(new { character.Id, character.Name, character.AvatarUrl });
            }
            catch (ImageGenException ex)
            {
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
        }
    }
    

}
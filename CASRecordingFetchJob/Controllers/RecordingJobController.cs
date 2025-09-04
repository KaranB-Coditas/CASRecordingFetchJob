using CASRecordingFetchJob.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CASRecordingFetchJob.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecordingJobController : ControllerBase
    {
        private readonly IRecordingJobService _recordingJobService;
        public RecordingJobController(IRecordingJobService recordingJobService)
        {
            _recordingJobService = recordingJobService;
        }
        [HttpGet("Execute")]
        public async Task<IActionResult> Execute([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] int companyId = 0, [FromQuery] int leadtransitId = 0)
        {
            var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                       ?? Guid.NewGuid().ToString();

            Response.Headers["X-Correlation-ID"] = correlationId;

            return await _recordingJobService.ExecuteRecordingJob(correlationId, startDate, endDate, companyId, leadtransitId);
        }

    }
}

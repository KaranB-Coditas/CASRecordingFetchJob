using CASRecordingFetchJob.Model;
using Microsoft.AspNetCore.Mvc;

namespace CASRecordingFetchJob.Services
{
    public interface IRecordingJobService
    {
        Task<IActionResult> ExecuteRecordingJob(string correlationId, DateTime? startDate = null, DateTime? endDate = null, int companyId = 0, int leadtransitId = 0);
    }
}

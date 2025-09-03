using CASRecordingFetchJob.Services;

namespace CASRecordingFetchJob.Helpers
{
    public class LoggerHelper
    {
        private readonly ILogger<RecordingJobService> _log;
        public LoggerHelper(ILogger<RecordingJobService> log)
        {
            _log = log;
        }
        public void Info(string message, string correlationId) => 
            _log.LogInformation("{Message} | CorrelationId: {CorrelationId} ", message, correlationId);

        public void Error(string message, Exception ex, string correlationId) =>
            _log.LogError(ex, "{Message} | CorrelationId: {CorrelationId}", message, correlationId);

        public void Warn(string message, string correlationId) =>
            _log.LogWarning("{Message} | CorrelationId: {CorrelationId} ", message, correlationId);

        public void Debug(string message, string correlationId) =>
            _log.LogDebug("{Message} | CorrelationId: {CorrelationId} ", message, correlationId);
    }
}

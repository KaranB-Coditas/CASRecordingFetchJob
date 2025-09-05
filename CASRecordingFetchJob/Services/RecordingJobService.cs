using CASRecordingFetchJob.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.ConstrainedExecution;
using FFMpegCore;
using FFMpegCore.Pipes;
using FFMpegCore.Enums;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text;
using System.ComponentModel.Design;
using CASRecordingFetchJob.Helpers;
using System.Security.AccessControl;
using Serilog.Core;
using System.Collections.Concurrent;

namespace CASRecordingFetchJob.Services
{
    public class RecordingJobService : IRecordingJobService
    {
        private readonly IConfiguration _config;
        private readonly RecordingJobDBContext _db;
        private readonly GoogleCloudStorageHelper _gcsHelper;
        private readonly LoggerHelper _logger;
        public RecordingJobService(
            IConfiguration config, 
            RecordingJobDBContext db, 
            GoogleCloudStorageHelper gcsHelper, 
            LoggerHelper logger) 
        {
            _config = config;
            _db = db;
            _gcsHelper = gcsHelper;
            _logger = logger;
        }
        public bool CheckRecordJobEnabled()
        {
            return _config.GetValue<bool?>("RecordingJobEnabled") ?? true;
        }
        public string GetRecordingsBasePath()
        {
            return _config.GetValue<string>("RecordingsBasePath") ?? string.Empty;
        }
        public async Task<IActionResult> ExecuteRecordingJob(
            string correlationId,
            DateTime? startDate = null, 
            DateTime? endDate = null, 
            int companyId = 0, 
            int leadtransitId = 0
            )
        {
            if(string.IsNullOrEmpty(correlationId))
                correlationId = Guid.NewGuid().ToString();

            var recordingJobResponse = new RecordingJobResponse
            {
                CorrelationId = correlationId
            };

            _logger.Info("Recording Job Started", correlationId);

            if (!CheckRecordJobEnabled())
            {
                _logger.Info("Recording Job Disabled, terminating recording job", correlationId);
                return new BadRequestObjectResult(new { error = "Recording job disabled" });
            }

            var companyIdsToProcess = await GetCompanyIdsAsync(correlationId, companyId, leadtransitId);
            if (companyIdsToProcess == null || companyIdsToProcess.Count == 0)
            {
                _logger.Info("No Company present for recording job", correlationId);
                return new BadRequestObjectResult(new { error = "No Company Ids present for recording job" });
            }
            recordingJobResponse.CompanyIdsToProcess = companyIdsToProcess;
            _logger.Info($"Company Ids to Process for Job {string.Join(", ", companyIdsToProcess)}", correlationId);

            var callDetails = await GetCallDetailsAsync(
                startDate ?? DateTime.Now.Date, 
                endDate ?? DateTime.Now.AddDays(1).Date, 
                companyIdsToProcess, 
                leadtransitId);

            if (callDetails == null || callDetails.Count == 0)
            {
                _logger.Info($"No Conversation Record Found", correlationId);
                return new BadRequestObjectResult(new { error = "No conversation record found" });
            }

            var agentInitiatedConversationPhoneCalls = await GetAgentInitiatedConversationPhoneCalls(callDetails, companyIdsToProcess);

            var conversationIds = callDetails
                .Where(a => a.CallType == 2)
                .Select(a => a.LeadtransitId)
                .Distinct()
                .ToList();

            if (conversationIds.Count == 0)
            {
                _logger.Info($"No matching calls", correlationId);
                return new BadRequestObjectResult(new { error = "No matching calls" });
            }
            recordingJobResponse.TotalConversationFetched = conversationIds.Count;
            _logger.Info($"Conversation Count {conversationIds.Count}", correlationId);

            var processDetails = new ConcurrentBag<RecordingDetails>();
            var successfulIds = new ConcurrentBag<int>();
            var failedIds = new ConcurrentBag<int>();
            var maxDegreeOfParallelism = _config.GetValue<int?>("MaxDegreeOfParallelism") ?? 1;

            await Parallel.ForEachAsync(conversationIds,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                async (id, cancellationToken) =>
                {
                    var recordingJobInfo = new RecordingDetails { LeadTransitId = id };
                    try
                    {
                        var agentTrimTime = 0;
                        var conversation = callDetails.Where(a => a.LeadtransitId == id).ToList();
                        var conversationDate = ConvertTimeZoneFromUtcToPST(conversation[0].LeadCatchTime);
                        var companyId = conversation[0].ClientId;

                        if (conversation.Count > 1)
                            agentTrimTime = GetAgentTrimTime(conversation, agentInitiatedConversationPhoneCalls ?? []);

                        _logger.Info($"[{companyId}] [{leadtransitId}] Started Processing Recording for LeadtransitId {id}, Conversation on {conversationDate} PST, Agent Trim Time {agentTrimTime} sec", correlationId);

                        string recordingsBasePath = Path.Combine(
                            GetRecordingsBasePath(),
                            conversationDate.Year.ToString(),
                            conversationDate.Month.ToString(),
                            conversationDate.Day.ToString());

                        string gcsRecordingPath = string.Join("/",
                            GetS3BucketName(),
                            conversationDate.Year.ToString(),
                            conversationDate.Month.ToString(),
                            conversationDate.Day.ToString());

                        Directory.CreateDirectory(recordingsBasePath);

                        string audioFileName = Path.Combine(recordingsBasePath, id.ToString() + GetSupportedAudioFormat());
                        if (File.Exists(audioFileName))
                        {
                            recordingJobInfo.IsFilePresent = true;
                            _logger.Info($"[{companyId}] [{leadtransitId}] File already exist - {id}{GetSupportedAudioFormat()}",correlationId);
                            successfulIds.Add(id);
                            return;
                        }

                        var cdrRecording = await FetchCdrRecordingAsync(
                            conversationDate.AddMinutes(-1),
                            conversationDate.AddMinutes(2),
                            GetDialedNumber(
                                conversation[0].PrimaryNumberIndex,
                                conversation[0].ContactTel1,
                                conversation[0].ContactTel2,
                                conversation[0].ContactTel3,
                                conversation[0].BestPhoneNumber),
                            correlationId,
                            id,
                            companyId);

                        if (cdrRecording == null)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] CDR recording not found", correlationId);
                            failedIds.Add(id);
                            return;
                        }
                        recordingJobInfo.IsFetchedFromCDR = true;

                        var convertedRecordings = await ConvertWavToMp3VariantsAsync(cdrRecording, id, correlationId, agentTrimTime, companyId);

                        if (convertedRecordings == null || convertedRecordings.Count == 0)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] Recording failed to convert", correlationId);
                            failedIds.Add(id);
                            return;
                        }
                        recordingJobInfo.IsConvertedToMp3Variants = true;

                        var MovingToContentServerResult = await MovingToContentServerTask(convertedRecordings, recordingsBasePath, correlationId, leadtransitId, companyId);
                        recordingJobInfo.IsMovedToContentServer = MovingToContentServerResult;

                        var MovingToGCSResult = await MovingToGCSTask(convertedRecordings, gcsRecordingPath, correlationId, leadtransitId, companyId);
                        recordingJobInfo.IsMovedToGCS = MovingToGCSResult;

                        if (!MovingToContentServerResult || !MovingToGCSResult)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] Recording failed to move to content server or GCS", correlationId);
                            failedIds.Add(id);
                            return;
                        }
                        successfulIds.Add(id);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{leadtransitId}] Error processing {id}: {ex.Message}", ex, correlationId);
                        failedIds.Add(id);
                    }
                    finally
                    {
                        processDetails.Add(recordingJobInfo);
                    }
                });

            recordingJobResponse.SuccessfulCount = successfulIds.Count;
            recordingJobResponse.FailedCount = failedIds.Count;
            recordingJobResponse.RecordingProcessDetails = processDetails.ToList();

            _logger.Info($"SuccessfulIds Count {successfulIds.Count}, FailedIds Count {failedIds.Count}",correlationId);
            _logger.Info($"FailedIds LeadtransitId {string.Join(", ", failedIds)}", correlationId);

            _logger.Info("Recording Job Ended", correlationId);
            return new OkObjectResult(recordingJobResponse);
        }

        public int GetAgentTrimTime(List<Conversation> conversation, Dictionary<int, DateTime> agentInitiatedConversationPhoneCalls)
        {
            var agentInitiatedConversationCallPlacedTime = 
                agentInitiatedConversationPhoneCalls.TryGetValue(conversation[0].LeadtransitId, out DateTime value) ? value : DateTime.MinValue;

            var agentConversation = conversation
                .Where(a => a.CallType == 1)
                .FirstOrDefault();

            return agentConversation == null
                ? 0
                : (agentInitiatedConversationCallPlacedTime != DateTime.MinValue)
                    ? (int)(agentInitiatedConversationCallPlacedTime - agentConversation.LeadCatchTime).TotalSeconds
                    : (int)(agentConversation.CallSendTime - agentConversation.LeadCatchTime).TotalSeconds;
        }

        public string GetS3BucketName()
        {
            return _config.GetValue<string>("S3RecordingBaseKey") ?? string.Empty;
        }

        public async Task<int> GetSeekTimeInSecondsAsync(int leadtransitId)
        {
            return  await _db.GetAgentCallTransferedTimeDifferenceAsync(leadtransitId) ?? 0;
        }

        public string GetSupportedAudioFormat()
        {
            var format = _config.GetValue<string>("SupportedAudioFormat") ?? ".mp3";
            if (!format.StartsWith('.'))
                format = "." + format;
            return format;
        }

        public async Task MovingToGCSAndContentServer(Dictionary<string, Stream> recordings, string recordingBasePath, string gcsRecordingPath, string correlationId, int leadtransitId, int companyId)
        {
            try
            {
                _logger.Info($"[{companyId}] [{leadtransitId}] Copy Recordings to GCS And Content Server", correlationId);

                var tasks = new List<Task<bool>>();

                foreach (var recording in recordings)
                {
                    tasks.Add(MoveRecordingsToContentServer(recording, recordingBasePath, correlationId, leadtransitId, companyId));
                    tasks.Add(MoveRecordingsToGCS(recording, gcsRecordingPath, correlationId, leadtransitId, companyId));
                }

                var result = await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] MovingToGCSAndContentServer Failed", ex, correlationId);
            }
            
        }

        public async Task<bool> MovingToGCSTask(Dictionary<string, Stream> recordings, string gcsRecordingPath, string correlationId, int leadtransitId, int companyId)
        {
            try
            {
                _logger.Info($"[{companyId}] [{leadtransitId}] Copy Recordings to GCS", correlationId);

                var tasks = new List<Task<bool>>();
                foreach (var recording in recordings)
                {
                    tasks.Add(MoveRecordingsToGCS(recording, gcsRecordingPath, correlationId, leadtransitId, companyId));
                }
                var results = await Task.WhenAll(tasks);

                return results.All(r => r);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] Recordings Copied to GCS Failed", ex, correlationId);
                return false;
            }

        }

        public async Task<bool> MovingToContentServerTask(Dictionary<string, Stream> recordings, string recordingBasePath, string correlationId, int leadtransitId, int companyId)
        {
            try
            {
                _logger.Info($"[{companyId}] [{leadtransitId}] Copy Recordings to Content Server", correlationId);

                var tasks = new List<Task<bool>>();
                foreach (var recording in recordings)
                {
                    tasks.Add(MoveRecordingsToContentServer(recording, recordingBasePath, correlationId, leadtransitId, companyId));
                }
                var results = await Task.WhenAll(tasks);

                return results.All(r => r);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] Recordings Copied to Content Server Failed", ex, correlationId);
                return false;
            }

        }

        public async Task<bool> MoveRecordingsToContentServer(KeyValuePair<string, Stream> recording, string recordingBasePath, string correlationId, int leadtransitId, int companyId)
        {
            try
            {
                var filePath = Path.Combine(recordingBasePath, recording.Key);

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                {
                    await recording.Value.CopyToAsync(fileStream);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] Recordings {recording.Key} Copied to Content Server Failed", ex, correlationId);
                return false;
            }
        }

        public async Task<bool> MoveRecordingsToGCS(KeyValuePair<string, Stream> recording, string gcsRecordingPath, string correlationId, int leadtransitId, int companyId)
        {
            try
            {
                var key = $"{gcsRecordingPath}/{recording.Key}";
                var contentType = GetSupportedAudioFormat() == ".mp3" ? "audio/mpeg" : "audio/wav";
                await _gcsHelper.UploadRecordingAsync(key, recording.Value, contentType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] Recordings {recording.Key} Copied to GCS Failed", ex, correlationId);
                return false;
            }
            
        }

        private async Task<Stream> ConvertWithFfmpegAsync(Stream inputStream, string correlationId, int companyId, int leadTransitId, Action<FFMpegArgumentOptions> configureOptions)
        {
            if (inputStream.CanSeek)
                inputStream.Position = 0;

            var outputStream = new MemoryStream();

            try
            {
                await FFMpegArguments
                    .FromPipeInput(new StreamPipeSource(inputStream))
                    .OutputToPipe(new StreamPipeSink(outputStream), options =>
                    {
                        configureOptions(options);
                        options.WithAudioCodec(AudioCodec.LibMp3Lame)
                               .ForceFormat("mp3");
                    })
                    .ProcessAsynchronously();

                outputStream.Position = 0;
                return outputStream;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadTransitId}] FFmpeg conversion failed", ex, correlationId);
                outputStream.Dispose();
                throw;
            }
        }

        public async Task<KeyValuePair<string, Stream>> ConvertToMp3(Stream wavStream, int leadTransitId, string correlationId, int companyId)
        {
            var normalStream = await ConvertWithFfmpegAsync(
                wavStream, correlationId, companyId, leadTransitId,
                options => { });

            return new KeyValuePair<string, Stream>($"{leadTransitId}.mp3", normalStream);
        }

        public async Task<KeyValuePair<string, Stream>> RemoveProspectChannelRecording(Stream wavStream, int leadTransitId, string correlationId, int companyId)
        {
            var monoStream = await ConvertWithFfmpegAsync(
                wavStream, correlationId, companyId, leadTransitId,
                options => options.WithCustomArgument("-af \"pan=mono|c0=c0\""));

            return new KeyValuePair<string, Stream>($"{leadTransitId}_pitcher.mp3", monoStream);
        }

        public async Task<KeyValuePair<string, Stream>> TrimAgentRecording(Stream wavStream, int leadTransitId, string correlationId, int companyId, int seekTime)
        {
            var trimmedStream = await ConvertWithFfmpegAsync(
                wavStream, correlationId, companyId, leadTransitId,
                options => options.WithCustomArgument($"-ss {seekTime}"));

            return new KeyValuePair<string, Stream>($"{leadTransitId}_trimmed.mp3", trimmedStream);
        }

        public async Task<Dictionary<string, Stream>> ConvertWavToMp3VariantsAsync(Stream? wavStream, int leadtransitId, string correlationId, int? seekTime = 0, int companyId=0)
        {
            var convertedRecordings = new Dictionary<string, Stream>();
            try
            {
                if (wavStream == null || wavStream.Length == 0)
                    throw new InvalidOperationException("Input stream is empty.");
                _logger.Info($"[{companyId}] [{leadtransitId}] Started Converting Recordins to Mp3", correlationId);

                byte[] wavBytes;
                using (var ms = new MemoryStream())
                {
                    await wavStream.CopyToAsync(ms);
                    wavBytes = ms.ToArray();
                }

                var task = new List<Task<KeyValuePair<string, Stream>>>
                {
                    ConvertToMp3(new MemoryStream(wavBytes), leadtransitId, correlationId, companyId),
                    RemoveProspectChannelRecording(new MemoryStream(wavBytes), leadtransitId, correlationId, companyId),
                    TrimAgentRecording(new MemoryStream(wavBytes), leadtransitId, correlationId, companyId, seekTime ?? 0)
                };

                var result = await Task.WhenAll(task);

                foreach (var kvp in result)
                    convertedRecordings[kvp.Key] = kvp.Value;

                _logger.Info($"[{companyId}] [{leadtransitId}] Ended Converting Recordins to Mp3", correlationId);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] Error = Convert Recordins To Mp3 Failed",ex, correlationId);
            }
            return convertedRecordings;

        }

        public string GetCdrRecordingsServerBasePath()
        {
            return _config.GetValue<string>("RecordingsServerBasePath") ?? string.Empty;
        }

        public Dictionary<string, string> GetCdrRecordingServerCredentials()
        {
            var credentials = new Dictionary<string, string>();
            credentials.Add("CdrUserName", _config.GetValue<string>("CdrUserName") ?? string.Empty);
            credentials.Add("CdrPassword", _config.GetValue<string>("CdrPassword") ?? string.Empty);
            return credentials;
        }

        public List<string> GetCallableNumbers(string called)
        {
            List<string> callableNumbers = [];

            called = called.Replace("+", "").Trim();

            if (called.FirstOrDefault() != '1')
            {
                callableNumbers.Add("011" + called);
                callableNumbers.Add("1" + called);
            }
            else
                callableNumbers.Add(called);

            return callableNumbers;
        }

        public string GetCdrRecordingUrl(DateTime startTimeFrom, DateTime startTimeTo, string called, string? cdrId)
        {
            var cdrRecordingsServerBasePath = GetCdrRecordingsServerBasePath();
            var cdrRecordingServerCredentials = GetCdrRecordingServerCredentials();

            if(!string.IsNullOrEmpty(cdrId))
                return cdrRecordingsServerBasePath + "api.php?task=getVoiceRecording&user=" + cdrRecordingServerCredentials["CdrUserName"] + "&password=" + cdrRecordingServerCredentials["CdrPassword"] + "&params={\"cdrId\":\"" + cdrId + "\"}";
            else
                return cdrRecordingsServerBasePath + "api.php?task=getVoipCalls&user=" + cdrRecordingServerCredentials["CdrUserName"] + "&password=" + cdrRecordingServerCredentials["CdrPassword"] + "&params={\"startTime\":\"" + startTimeFrom + "\",\"startTimeTo\":\"" + startTimeTo + "\",\"called\":\"" + called + "\"}";
        }

        public async Task<Stream?> FetchCdrRecordingAsync(DateTime startTimeFrom, DateTime startTimeTo, string called, string correlationId,int leadtransitId = 0, int companyId = 0)
        {
            try
            {
                foreach (var callableNumber in GetCallableNumbers(called))
                {
                    _logger.Info($"[{companyId}] [{leadtransitId}] Fetch Recording from CDR, Callable Number {callableNumber}", correlationId);
                    var cdrUrl = GetCdrRecordingUrl(startTimeFrom, startTimeTo, callableNumber, null);
                    _logger.Info($"[{companyId}] [{leadtransitId}] CDR Url {cdrUrl}", correlationId);
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync(cdrUrl);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] CDR Response Status Code {response.StatusCode}", correlationId);
                            return null;
                        }

                        var responseBody = await response.Content.ReadAsStringAsync();
                        var cdrResponse = JsonConvert.DeserializeObject<CdrResponse>(responseBody);

                        if (cdrResponse == null || cdrResponse.Cdr == null || cdrResponse.Cdr.Count == 0)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] CDR Response Empty", correlationId);
                            continue;
                        }

                        string cdrId = string.Empty;
                        foreach (var cdr in cdrResponse.Cdr)
                        {
                            cdrId = cdr.CdrId;
                        }

                        _logger.Info($"[{companyId}] [{leadtransitId}] Found CDR, cdr id {cdrId}", correlationId);

                        var recordingUrl = GetCdrRecordingUrl(startTimeFrom, startTimeTo, callableNumber, cdrId);

                        _logger.Info($"[{companyId}] [{leadtransitId}] Download CDR recording url {recordingUrl}", correlationId);

                        var cdrRecordingResponse = await httpClient.GetAsync(recordingUrl);

                        var contentType = cdrRecordingResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;

                        var bytes = await cdrRecordingResponse.Content.ReadAsByteArrayAsync();

                        if (string.Equals(contentType, "audio/x-wav", StringComparison.OrdinalIgnoreCase) && bytes != null)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] Downloaded CDR recording", correlationId);
                            var memoryStream = new MemoryStream(bytes)
                            {
                                Position = 0
                            };
                            return memoryStream;
                        }
                        else if (string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase) && bytes != null)
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] Download CDR Recording Failed", correlationId);
                            string responseText = Encoding.UTF8.GetString(bytes);

                            using var doc = JsonDocument.Parse(responseText);
                            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                                errors.TryGetProperty("reason", out var reason))
                            {
                                var error = reason.GetString();
                                _logger.Info($"[{companyId}] [{leadtransitId}] Download Failed Reason {error}", correlationId);
                            }
                            return null;
                        }
                        else
                        {
                            _logger.Info($"[{companyId}] [{leadtransitId}] Something went wrong cdr recording failed to download, contentType {contentType}, is Downloaded Stream Empty {bytes == null}", correlationId);
                            return null;
                        }
                    }
                }
                _logger.Info($"[{companyId}] [{leadtransitId}] No valid callable number found, dialed number {called}", correlationId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{companyId}] [{leadtransitId}] No valid callable number found, dialed number {called}", ex, correlationId);
                return null;
            }
        }

        public async Task<Stream?> DownloadRecordingAsync(string recordingUrl)
        {
            try
            {
                using var httpClient = new HttpClient();

                var fileBytes = await httpClient.GetByteArrayAsync(recordingUrl);

                var memoryStream = new MemoryStream(fileBytes);

                memoryStream.Position = 0;
                string outputDirectory = "C:\\Recordings";
                string fileName = DateAndTime.Now.Minute +"_temp_recording.wav";
                string filePath = Path.Combine(outputDirectory, fileName);

                await File.WriteAllBytesAsync(filePath, fileBytes);

                return memoryStream;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public DateTime ConvertTimeZoneFromUtcToPST(DateTime utcDateTime)
        {
            TimeZoneInfo PacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var pstDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, PacificZone);
            return pstDateTime;
        }

        public string GetDialedNumber(int dialingIndex, string phone1, string phone2, string phone3, string phone4)
        {
            var dialedNumber = string.Empty;
            switch (dialingIndex)
            {
                case 0:
                    dialedNumber = phone1;
                    break;
                case 1:
                    dialedNumber = phone2;
                    break;
                case 4:
                    dialedNumber = phone3;
                    break;
                case 5:
                    dialedNumber = phone4;
                    break;
                default:
                    dialedNumber = phone1;
                    break;
            }
            return dialedNumber;
        }

        public async Task<Dictionary<int, DateTime>> GetPhoneCalls(List<int> leadtransitIds)
        {
            if(leadtransitIds == null || leadtransitIds.Count == 0)
                return [];
            try
            {
                return await _db.t_PhoneCall
                    .Where(pc => leadtransitIds.Contains(pc.LeadTransitId))
                    .OrderByDescending(pc => pc.CallPlacedTime)
                    .GroupBy(pc => pc.LeadTransitId)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.Max(x => x.CallPlacedTime) ?? DateTime.Now
                    );
            }
            catch (Exception)
            {
                return [];
            }
        }

        public async Task<Dictionary<int, DateTime>> GetAgentInitiatedConversationPhoneCalls(List<Conversation> conversations, List<int> companyIds)
        {
            if (conversations == null || companyIds == null)
                return [];

            var agentInitiatedCallEnabledCompanyIds = await GetAgentInitiatedCallEnabledCompanies(companyIds);

            var agentInitiatedConversation = conversations
                .Where(a => agentInitiatedCallEnabledCompanyIds.Contains(a.ClientId))
                .Select(a => a.LeadtransitId)
                .Distinct()
                .ToList();

            if(agentInitiatedConversation == null || agentInitiatedConversation.Count == 0)
                return [];

            return await GetPhoneCalls(agentInitiatedConversation);
        }

        public async Task<List<Conversation>> GetCallDetailsAsync(DateTime startDate, DateTime endDate, List<int> clientIds, int leadtransitId = 0)
        {
            if (leadtransitId > 0)
                return await GetCallDetailsByLeadtransitIdAsync(leadtransitId);

            try
            {
                var query = from note in _db.cas_Note
                            join call in _db.t_Call
                                on note.LeadTransitId equals call.LeadTransitId
                            where note.CreateDate > startDate
                                  && note.CreateDate < endDate
                            orderby call.ClientId, call.LeadCatchTime
                            select new Conversation
                            {
                                LeadtransitId = note.LeadTransitId ?? 0,
                                LeadCatchTime = call.LeadCatchTime ?? DateTime.MinValue,
                                CallSendTime = call.CallSendTime ?? DateTime.MinValue,
                                PrimaryNumberIndex = call.PrimaryNumberIndex ?? 0,
                                ContactTel1 = call.ContactTel1 ?? string.Empty,
                                ContactTel2 = call.ContactTel2 ?? string.Empty,
                                ContactTel3 = call.ContactTel3 ?? string.Empty,
                                BestPhoneNumber = call.BestPhoneNumber ?? string.Empty,
                                CallType = call.CallType ?? 0,
                                TalkTime = call.TalkTime ?? 0,
                                ClientId = call.ClientId ?? 0,
                            };

                if (clientIds != null)
                    query = query.Where(x => clientIds.Contains(x.ClientId));

                return await query.ToListAsync();
            }
            catch (Exception)
            {
                return [];
            }
            
        }

        public async Task<List<Conversation>> GetCallDetailsByLeadtransitIdAsync(int leadtransitId)
        {
            try
            {
                var query = from note in _db.cas_Note
                            join call in _db.t_Call
                                on note.LeadTransitId equals call.LeadTransitId
                            where note.LeadTransitId == leadtransitId
                            select new Conversation
                            {
                                LeadtransitId = note.LeadTransitId ?? 0,
                                LeadCatchTime = call.LeadCatchTime ?? DateTime.MinValue,
                                CallSendTime = call.CallSendTime ?? DateTime.MinValue,
                                PrimaryNumberIndex = call.PrimaryNumberIndex ?? 0,
                                ContactTel1 = call.ContactTel1 ?? string.Empty,
                                ContactTel2 = call.ContactTel2 ?? string.Empty,
                                ContactTel3 = call.ContactTel3 ?? string.Empty,
                                BestPhoneNumber = call.BestPhoneNumber ?? string.Empty,
                                CallType = call.CallType ?? 0,
                                TalkTime = call.TalkTime ?? 0,
                                ClientId = call.ClientId ?? 0
                            };
                return await query.ToListAsync();
            }
            catch (Exception)
            {

                return [];
            }
        }

        public async Task<List<int>> GetAgentInitiatedCallEnabledCompanies(List<int> allCompany)
        {

            return await _db.cas_CompanySetting
                .Where(a => a.SettingKey == "DisableAutomation" && a.SettingValue == Boolean.TrueString && allCompany.Contains(a.CompanyId))
                .Select(a => a.CompanyId)
                .ToListAsync();
        }

        public async Task<int> GetCompanyIdByLeadTransitId(int leadtransitId)
        {
            try
            {
                var companyId = await _db.t_Call
                    .Where(a => a.LeadTransitId == leadtransitId)
                    .Select(a => a.ClientId)
                    .FirstOrDefaultAsync();
                return companyId ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public async Task<List<int>> GetCompanyIdsAsync(string correlationId, int companyId = 0, int leadtransitId = 0)
        {
            _logger.Info("Fetching company ids to process recording job", correlationId);
            if (companyId > 0)
                return [companyId];
            if(leadtransitId > 0)
                return [await GetCompanyIdByLeadTransitId(leadtransitId)];

            var activeCompanyIds = new List<int>();
            try
            {
                activeCompanyIds = await _db.t_Company
                .Where(a => !a.IsDeleted)
                .Select(a => a.ID)
                .ToListAsync();

                var disableRecordingDownloadFromJobCompanyIds = await _db.cas_CompanySetting
                    .Where(a => a.SettingKey == "DisableRecordingDownloadFromJob" && a.SettingValue == Boolean.TrueString)
                    .Select(a => a.CompanyId)
                    .ToListAsync();

                _logger.Info($"DisableRecordingDownloadFromJob CompanyIds {string.Join(", ", disableRecordingDownloadFromJobCompanyIds)} ", correlationId);

                var realTimeRecordingEnabledCompanyIds = await _db.cas_CompanySetting
                    .Where(a => a.SettingKey == "EnableRealTimeRecording" && a.SettingValue == Boolean.TrueString)
                    .Select(a => a.CompanyId)
                    .ToListAsync();

                _logger.Info($"EnableRealTimeRecording CompanyIds {string.Join(", ", realTimeRecordingEnabledCompanyIds)} ", correlationId);

                var removeCompanyIds = disableRecordingDownloadFromJobCompanyIds.Union(realTimeRecordingEnabledCompanyIds).ToList();

                _logger.Info($"Removed Company Ids from Job {string.Join(", ", disableRecordingDownloadFromJobCompanyIds)} ", correlationId);

                return activeCompanyIds.Except(removeCompanyIds).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in {nameof(GetCompanyIdsAsync)}", ex, correlationId);
                return activeCompanyIds;
            }
            
        }
    }
}

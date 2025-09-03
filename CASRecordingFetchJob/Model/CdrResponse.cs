using Newtonsoft.Json;
using System.Runtime.ConstrainedExecution;

namespace CASRecordingFetchJob.Model
{
    public class CdrResponse
    {
        [JsonProperty("cdr")]
        public List<Cdr> Cdr { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("_debug")]
        public bool Debug { get; set; }
    }

    public class Cdr
    {
        [JsonProperty("cdrId")]
        public string CdrId { get; set; }

        [JsonProperty("caller")]
        public string Caller { get; set; }

        [JsonProperty("called")]
        public string Called { get; set; }

        [JsonProperty("calldate")]
        public DateTime CallDate { get; set; }

        [JsonProperty("callend")]
        public DateTime CallEnd { get; set; }

        [JsonProperty("callId")]
        public string CallId { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("connect_duration")]
        public int ConnectDuration { get; set; }

        [JsonProperty("sipcalledport")]
        public string SipCalledPort { get; set; }

        [JsonProperty("sipcalledip")]
        public string SipCalledIp { get; set; }

        [JsonProperty("sipcallerip")]
        public string SipCallerIp { get; set; }

        [JsonProperty("sipcallerport")]
        public string SipCallerPort { get; set; }

        [JsonProperty("codec_a")]
        public string CodecA { get; set; }

        [JsonProperty("codec_b")]
        public string CodecB { get; set; }
    }
}

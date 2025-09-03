namespace CASRecordingFetchJob.Model
{
    public class Conversation
    {
        public int LeadtransitId { get; set; }
        public DateTime LeadCatchTime { get; set; }
        public DateTime CallSendTime { get; set; }
        public int PrimaryNumberIndex { get; set; }
        public string ContactTel1 { get; set; }
        public string ContactTel2 { get; set; }
        public string ContactTel3 { get; set; }
        public string BestPhoneNumber { get; set; }
        public byte CallType { get; set; }
        public int TalkTime { get; set; }
        public int ClientId { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace CASRecordingFetchJob.Model
{
    public class Call
    {
        public int ID { get; set; }
        public int? LeadId { get; set; }
        public int? LeadTransitId { get; set; }
        public string? ClientName { get; set; }
        public string? UserName { get; set; }
        public string? CampaignName { get; set; }
        public string? SessionName { get; set; }
        public string? LeadListName { get; set; }
        public DateTime? ThrowTime { get; set; }
        public DateTime? LeadCatchTime { get; set; }
        public DateTime? CallSendTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? EngineCallSignalTime { get; set; }
        public DateTime? EngineCallConnectTime { get; set; }
        public DateTime? PhoneSysConnectTime { get; set; }
        public int? TalkTime { get; set; }
        public int? FailedCalls { get; set; }
        public int? Redials { get; set; }
        public string? NoteText { get; set; }
        public string? DispositionText { get; set; }
        public int? ProblemCodes { get; set; }
        public string? ProblemComments { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyURL { get; set; }
        public string? CompanyTel1 { get; set; }
        public string? CompanyExt1 { get; set; }
        public string? CompanyTel2 { get; set; }
        public string? CompanyExt2 { get; set; }
        public string? CompanyAddr1 { get; set; }
        public string? CompanyAddr2 { get; set; }
        public string? CompanyCity { get; set; }
        public string? CompanyState { get; set; }
        public string? CompanyZip { get; set; }
        public string? CompanyCountry { get; set; }
        public string? CompanyInfo { get; set; }
        public string? ContactSalutation { get; set; }
        public string? ContactFirstName { get; set; }
        public string? ContactLastName { get; set; }
        public string? ContactJobTitle { get; set; }
        public string? ContactDept { get; set; }
        public string? ContactTel1 { get; set; }
        public string? ContactExt1 { get; set; }
        public string? ContactTel2 { get; set; }
        public string? ContactExt2 { get; set; }
        public string? ContactAddr1 { get; set; }
        public string? ContactAddr2 { get; set; }
        public string? ContactCity { get; set; }
        public string? ContactState { get; set; }
        public string? ContactZip { get; set; }
        public string? ContactCountry { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactBackground { get; set; }
        public int? PrimaryNumberIndex { get; set; }
        public int? AvgLatency { get; set; }
        public DateTime? NameCatchTime { get; set; }
        public DateTime? LeadSendTime { get; set; }
        public bool IsDeleted { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public DateTime CreateDate { get; set; }
        public string CreateUser { get; set; } = string.Empty;
        public DateTime ModDate { get; set; }
        public string ModUser { get; set; } = string.Empty;
        public int UserRole { get; set; }
        public bool? LeadActive { get; set; }
        public string? CrmId { get; set; }
        public DateTime? PopupTime { get; set; }
        public DateTime? CachedLeadDisplayTime { get; set; }
        public DateTime? ActualLeadDisplayTime { get; set; }
        public int? SessionId { get; set; }
        public bool VoicemailLeft { get; set; }
        public int NumVoicemails { get; set; }
        public bool TransferHangup { get; set; }
        public bool IsValidConnect { get; set; }
        public int UserPostAction { get; set; }
        public string Referrer { get; set; } = string.Empty;
        public int? ContactId { get; set; }
        public int? CallListId { get; set; }
        public int? WaitTime { get; set; }
        public bool IsConversation { get; set; }
        public short? DispositionType { get; set; }
        public bool IsBlindLead { get; set; }
        public bool IsGracefulExit { get; set; }
        public bool? IsConnectOnHello { get; set; }
        public string? DialedProbableDirectNumber { get; set; }
        public int? AutoAgentCode { get; set; }
        public bool IsMeetingSet { get; set; }
        public bool IsFollowUpMarked { get; set; }
        public int? TalkTimeInMilliseconds { get; set; }
        public bool? WasPredictedAsTransfer { get; set; }
        public bool VoicemailType { get; set; }
        public string? SpecialInstructions { get; set; }
        public string? CallAlert { get; set; }
        public bool? IsCOLV { get; set; }
        public bool FollowUpHappened { get; set; }
        public int? UserId { get; set; }
        public int? CampaignUserId { get; set; }
        public byte? SessionType { get; set; }
        public int? DispositionId { get; set; }
        public byte? IsTransfer { get; set; }
        public int? ClientId { get; set; }
        public byte? CallType { get; set; }
        public byte? IsReferral { get; set; }
        public string? ContactExt3 { get; set; }
        public string? ContactTel3 { get; set; }
        public int AgentWrapTime { get; set; }
        public bool IsPitcherBusy { get; set; }
        public bool IsTransferOnPitcherBusy { get; set; }
        public bool LeaveVoicemail { get; set; }
        public double? FpnScore { get; set; }
        public string? RecordingInterval { get; set; }
        public bool IsProblemCall { get; set; }
        public byte IsValidCall { get; set; }
        public string? CleansedCompanyName { get; set; }
        public string? SyntheticContactId { get; set; }
        public bool IsVMCallbackLogged { get; set; }
        public bool IsClickToDial { get; set; }
        public string CallerId { get; set; } = string.Empty;
        public string? CrmId2 { get; set; }
        public string? BestPhoneNumber { get; set; }
    }

}

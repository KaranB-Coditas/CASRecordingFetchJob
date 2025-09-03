using System.ComponentModel.DataAnnotations;

namespace CASRecordingFetchJob.Model
{
    public class PhoneCall
    {
        public int Id { get; set; }
        public DateTime? CallPlacedTime { get; set; }
        public DateTime? CallConnectedTime { get; set; }
        public DateTime? CallDisconnectedTime { get; set; }
        public DateTime? StartNavCompanyIVRTime { get; set; }
        public DateTime? StartNavDBNIVRTime { get; set; }
        public bool WasAnsweredByPerson { get; set; }
        public bool WasAnsweredByIVR { get; set; }
        public bool DidProspectAnswer { get; set; }
        public string DialedNumber { get; set; }
        public string CallerId { get; set; }
        public int ContactId { get; set; }
        public int PhoneNumberId { get; set; }
        public bool IsDeleted { get; set; }
        public byte[] RowVersion { get; set; }
        public DateTime CreateDate { get; set; }
        public string CreateUser { get; set; }
        public DateTime ModDate { get; set; }
        public string ModUser { get; set; }
        public bool IsDialerSession { get; set; }
        public DateTime UserAttachTime { get; set; }
        public DateTime UserBridgeTime { get; set; }
        public int LeadTransitId { get; set; }
        public string Channel { get; set; }
        public DateTime? StartNavAdditionalDBNIVRTime { get; set; }
        public int? ReportCodes { get; set; }
        public string ReportComments { get; set; }
        public int SessionId { get; set; }
        public bool AgentLockedOnConnect { get; set; }
        public string TerminationCode { get; set; }
        public bool IsAMD { get; set; }
        public string AMDResult { get; set; }
        public string IVRPath { get; set; }
        public string PhoneNumberIVRPath { get; set; }
        public string AdditionalIVRPath { get; set; }
        public string IVRPathNote { get; set; }
        public bool IsFax { get; set; }
        public bool IsIVR { get; set; }
        public bool IsDirect { get; set; }
        public bool IsIVRPathValidated { get; set; }
        public bool AutoNavigateIVR { get; set; }
        public string AutomationFlow { get; set; }
        public string AutomationResult { get; set; }
        public string Transcript { get; set; }
        public string IvrNavResult { get; set; }
        public string AgentConnectReason { get; set; }
        public bool IsMobile { get; set; }
        public bool IsAmdEligibleForAgentInitiatedDialing { get; set; }
        public int OkClickWrapTimeForAID { get; set; }
        public string BeepDetected { get; set; }
        public string HangUpCauseCode { get; set; }
    }

}

namespace CASRecordingFetchJob.Model
{
    public class CompanySetting
    {
        public int CompanySettingId { get; set; }
        public int CompanyId { get; set; }
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string? LastModUser { get; set; }
        public DateTime? LastModDate { get; set; }
    }

}

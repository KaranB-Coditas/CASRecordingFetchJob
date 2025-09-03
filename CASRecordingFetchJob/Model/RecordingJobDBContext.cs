using Microsoft.EntityFrameworkCore;

namespace CASRecordingFetchJob.Model
{
    public class RecordingJobDBContext : DbContext
    {
        public RecordingJobDBContext(DbContextOptions<RecordingJobDBContext> options)
            : base(options) { }

        public DbSet<Call> t_Call { get; set; }
        public DbSet<Note> cas_Note { get; set; }
        public DbSet<PhoneCall> t_PhoneCall { get; set; }
        public DbSet<Company> t_Company { get; set; }
        public DbSet<CompanySetting> cas_CompanySetting { get; set; }

        public int? GetAgentCallTransferedTimeDifference(int leadTransitId)
        {
            var result = this.Database
                .SqlQueryRaw<int>(
                    "EXEC [dbo].[p_GetAgentCallTransferedTimeDifference] @LeadTransitID = {0}",
                    leadTransitId)
                .ToList();

            return result.FirstOrDefault();
        }

        public async Task<int?> GetAgentCallTransferedTimeDifferenceAsync(int leadTransitId)
        {
            var result = await this.Database
                .SqlQueryRaw<int>(
                    "EXEC [dbo].[p_GetAgentCallTransferedTimeDifference] @LeadTransitID = {0}",
                    leadTransitId)
                .ToListAsync();

            return result.FirstOrDefault();
        }
    }
}

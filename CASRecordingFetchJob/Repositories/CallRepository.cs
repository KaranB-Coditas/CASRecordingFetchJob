using System.Data;

namespace CASRecordingFetchJob.Repositories
{
    public class CallRepository
    {
        private readonly IDbConnection _connection;

        public CallRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        //public IEnumerable<Customer> GetAll()
        //{
        //    var sql = _
        //    return _connection.Query<Customer>("SELECT * FROM t_call");
        //}
    }
}

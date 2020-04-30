using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Database
{
    public static class Soccer
    {
        public static Dictionary<int, string> GetTeamNames(IEnumerable<int> uniqueIds)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                return cntx.GetTable<teamName>().Where(x => uniqueIds.Contains(x.id))
                    .ToDictionary(x => x.id, x => x.name);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace FinalBet.Database
{
    public static class Soccer
    {
        public static Dictionary<int, string> GetTeamNames(IEnumerable<match> matches)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var uniqueIds = (matches.Select(x => x.homeTeamId).Union(matches.Select(x => x.guestTeamId))).Distinct();

                return GetTeamNames(uniqueIds);
            }
        }

        public static Dictionary<int, string> GetTeamNames(IEnumerable<int> uniqueIds)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                return cntx.GetTable<teamName>().Where(x => uniqueIds.Contains(x.id)).
                    OrderBy(x => x.name)
                    .ToDictionary(x => x.id, x => x.name);
            }
        }
    }
}

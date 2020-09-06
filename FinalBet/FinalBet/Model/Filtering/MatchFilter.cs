using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;

namespace FinalBet.Model.Filtering
{
    public class LineImatch
    {
        public int LeagueUrlId { get; }
        public int TeamId { get; }
        public List<match> Data { get; }

        public LineImatch(int leagueUrlId, int teamId, List<match> data)
        {
            LeagueUrlId = leagueUrlId;
            TeamId = teamId;
            Data = data;
        }
    }


    public class MatchFilter
    {
        private List<LineImatch> Data { get; set; }

        public void DoFilter()
        {

        }

        public MatchFilter(leagueUrl url)
        {
            Data = new List<LineImatch>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var ordered = cntx.GetTable<match>().OrderBy(x => x.date);
                var teamIds = ((from m in cntx.GetTable<match>()
                    where m.leagueUrlId == url.id
                    select m.homeTeamId).Union(
                    from m in cntx.GetTable<match>()
                    where m.leagueUrlId == url.id
                    select m.guestTeamId)).Distinct().ToList();

                var l1 = (from m in ordered
                    where m.leagueUrlId == url.id && (teamIds.Contains(m.homeTeamId) || teamIds.Contains(m.guestTeamId))
                    
                    group m by new
                    {
                        m.leagueUrlId,
                        m.homeTeamId
                    }
                    into grp
                    select new LineImatch(grp.Key.leagueUrlId, grp.Key.homeTeamId, grp.ToList())).ToList();

            }

        }
    }
}

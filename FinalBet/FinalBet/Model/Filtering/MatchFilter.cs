using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;

namespace FinalBet.Model.Filtering
{
    public class MatchFilter
    {
        private List<List<IMatch>> Data { get; set; }

        public void DoFilter()
        {

        }

        public MatchFilter(league lg)
        {
            Data = new List<List<IMatch>>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var parentId = cntx.GetTable<leagueUrl>().Single(x => x.parentId == lg.id).id;

                /*
                var matchList = from m in cntx.GetTable<match>()
                                where m.leagueId == parentId
                                group m by new
                                {
                                    m.leagueUrlId,
                                    c.Friend,
                                    c.FavoriteColor,
                                } into gcs*/
            }

        }
    }
}

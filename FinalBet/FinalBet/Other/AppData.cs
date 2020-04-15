using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;

namespace FinalBet.Other
{
    public static class AppData
    {
        private static ObservableCollection<leagueMark> _leagueMarks = null;
        public static ObservableCollection<leagueMark> LeagueMarks
        {
            get
            {
                if (_leagueMarks != null) return _leagueMarks;

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var table = cntx.GetTable<leagueMark>().ToList();

                    _leagueMarks = new ObservableCollection<leagueMark>(table);
                }
                
                return _leagueMarks;
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace FinalBet.Model
{
    public class RGmatch:IMatch
    {
        public bool IsNa { get; set; }
        public bool IsNaN { get; set; }

        public bool IsHome { get; set; }
        public int Scored { get; set; }
        public int Missed { get; set; }
        public int Total { get; set; }
        public int Dif { get; set; }
        public Dictionary<string, double> Odds { get; set; }

        public int MatchId { get; set; }
        public Output Output { get; set; }

        public RGmatch(bool isHome, int scored, int missed, int total, int dif, IEnumerable<KeyValuePair<string,double>> odds)
        {
            IsNa = false;
            IsHome = isHome;
            Scored = scored;
            Missed = missed;
            Total = total;
            Dif = dif;
            if (odds != null && odds.Any())
            {
                Odds = new Dictionary<string, double>();
                foreach (var keyValuePair in odds)
                {
                    Odds.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }

        public RGmatch(bool isHome, int? resId, List<possibleResult> results, Dictionary<string,double> matchOdds)
        {
            Odds = new Dictionary<string, double>();
            if (resId == null)
            {
                IsNaN = false;
                IsNa = true;
                IsHome = true;
                Scored = -1;
                Missed = -1;
                Total = -1;
                Dif = -1;
                return;
            }

            var res = results.Single(x => x.id == resId);
            if (!res.isCorrect)
            {
                IsNaN = true;
                IsNa = false;
                IsHome = true;
                Scored = -1;
                Missed = -1;
                Total = -1;
                Dif = -1;
                return;
            }

            IsHome = isHome;
            Scored = res.scored;
            Missed = res.missed;
            Total = res.total;
            Dif = res.diff;
            if (matchOdds != null && matchOdds.Any())
            {
                Odds = new Dictionary<string, double>();
                foreach (var keyValuePair in matchOdds)
                {
                    Odds.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }
    }
}
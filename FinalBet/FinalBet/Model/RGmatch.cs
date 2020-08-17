using System.Collections.Generic;
using System.Linq;

namespace FinalBet.Model
{
    public class RGmatch:IMatch
    {
        public bool IsEmpty { get; set; }
        public bool IsNull { get; set; }

        public bool IsHome { get; set; }
        public int Scored { get; set; }
        public int Missed { get; set; }
        public Dictionary<string, double> Odds { get; set; }

        public int MatchId { get; set; }
        public Output Output { get; set; }

        public RGmatch(bool isHome, int scored, int missed, IEnumerable<KeyValuePair<string,double>> odds)
        {
            IsEmpty = false;
            IsHome = isHome;
            if (scored >= 0 && missed >= 0)
            {
                Scored = scored;
                Missed = missed;
            }
            else
            {
                IsNull = true;
                Scored = -1;
                Missed = -1;
            }

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
                IsNull = false;
                IsEmpty = true;
                IsHome = true;
                Scored = -1;
                Missed = -1;
                return;
            }

            var res = results.Single(x => x.id == resId);
            if (!res.isCorrect)
            {
                IsNull = true;
                IsEmpty = false;
                IsHome = true;
                Scored = -1;
                Missed = -1;
                return;
            }

            IsHome = isHome;
            Scored = res.scored;
            Missed = res.missed;
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
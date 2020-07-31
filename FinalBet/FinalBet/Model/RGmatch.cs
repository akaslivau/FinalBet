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

        public static RGmatch GetEmpty()
        {
            var res = new RGmatch(true, -1, -1, -1, -1, null) {IsNa = true};
            return res;
        }

        public static RGmatch GetNanMatch()
        {
            var res = new RGmatch(true, -1, -1, -1, -1, null) {IsNaN = true, IsNa = false};
            return res;
        }
    }
}
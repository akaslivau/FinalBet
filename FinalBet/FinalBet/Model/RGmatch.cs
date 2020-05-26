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

        public RGmatch(bool isHome, int scored, int missed, int total, int dif)
        {
            IsNa = false;
            IsHome = isHome;
            Scored = scored;
            Missed = missed;
            Total = total;
            Dif = dif;
        }

        public static RGmatch GetEmpty()
        {
            var res = new RGmatch(true, -1, -1, -1, -1) {IsNa = true};
            return res;
        }

        public static RGmatch GetNanMatch()
        {
            var res = new RGmatch(true, -1, -1, -1, -1) {IsNaN = true, IsNa = false};
            return res;
        }
    }
}
namespace FinalBet.Database
{
    public sealed class BeOddLoadMode
    {
        private readonly string _name;

        public static readonly BeOddLoadMode _1X2 = new BeOddLoadMode("1x2");
        public static readonly BeOddLoadMode OU = new BeOddLoadMode("ou");
        public static readonly BeOddLoadMode AH = new BeOddLoadMode("ah");
        public static readonly BeOddLoadMode BTS = new BeOddLoadMode("bts");

        private BeOddLoadMode(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }

    }
}
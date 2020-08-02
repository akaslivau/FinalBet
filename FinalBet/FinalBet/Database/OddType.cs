using System;

namespace FinalBet.Database
{
    public sealed class OddType
    {
        private readonly string _name;

        public static readonly OddType _1 = new OddType("1");
        public static readonly OddType X = new OddType("X");
        public static readonly OddType _2 = new OddType("2");

        public static readonly OddType Over = new OddType("Over");
        public static readonly OddType Under = new OddType("Under");

        public static readonly OddType Home = new OddType("Home");
        public static readonly OddType Guest = new OddType("Guest");

        public static readonly OddType BTS = new OddType("BTS");
        public static readonly OddType BTS_YES = new OddType("BTS_YES");
        public static readonly OddType BTS_NO = new OddType("BTS_NO");

        #region string methods

        public static string GetClearType(string typeAndPrm)
        {
            return typeAndPrm.Split(' ')[0];
        }

        public static string GetHomeOddType(double fora)
        {
            return Home + " " + fora;
        }

        public static string GetGuestOddType(double fora)
        {
            return Guest + " " + fora;
        }

        public static string GetHomeOddType(string fora)
        {
            return Home + " " + fora;
        }

        public static string GetGuestOddType(string fora)
        {
            return Guest + " " + fora;
        }

        public static string GetOverOddType(double total)
        {
            return Over + " " + total;
        }

        public static string GetUnderOddType(double total)
        {
            return Under + " " + total;
        }

        public static string GetOddTypeKeyword(BeOddLoadMode mode)
        {
            if (mode == BeOddLoadMode.OU) return "Over";
            if (mode == BeOddLoadMode.AH) return "Home";
            if (mode == BeOddLoadMode.BTS) return "BTS";
            throw new ArgumentException("Ty pes!");
        }
        #endregion

        private OddType(string name)
        {
            this._name = name;
        }

        public override string ToString()
        {
            return _name;
        }

        public static implicit operator string(OddType o) => o._name;


    }
}
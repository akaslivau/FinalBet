namespace FinalBet.Database
{
    public sealed class OddType
    {
        private readonly string _name;

        public static readonly OddType _1 = new OddType("1");
        public static readonly OddType X = new OddType("X");
        public static readonly OddType _2 = new OddType("2");
        
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
namespace FinalBet.Model.Filtering
{
    public static class StaticMatchMethods
    {
        [Filter(Id=0,Description = "Текущая длина", IsHistorical = false, NeedSolveMode = true)]
        public static double GetCurrentLength(IMatch m)
        {
            return 2;
        }

        [Filter(Id=1,Description = "Текущее число исходов", IsHistorical = false, NeedSolveMode = true)]
        public static double GetOutputCount(IMatch m)
        {
            return 4;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public static class StaticMatchMethods
    {
        [FilterAttribute(Description = "Текущая длина", IsHistorical = false, NeedSolveMode = true)]
        public static double GetCurrentLength(IMatch m)
        {
            return 2;
        }

        [FilterAttribute(Description = "Текущее число исходов", IsHistorical = false, NeedSolveMode = true)]
        public static double GetOutputCount(IMatch m)
        {
            return 4;
        }
    }
}

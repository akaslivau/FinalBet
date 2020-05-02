using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public static class MatchSolver
    {
        //Если вдруг будет тормозить
        private static Dictionary<string, Output> _cachedOutputs = new Dictionary<string, Output>();

        public static Output Solve(IMatch match, SolveMode mode, bool useCache = false)
        {
            //Тут жесткая привязка к режимам, че поделаешь
            var num = mode.SelectedMode.number;

            //Total
            if (num == 0)
                return match.Total > mode.ModeParameter
                    ? Output.Win
                    : (match.Total < mode.ModeParameter ? Output.Lose : Output.Deuce);

            //Fora
            if (num == 1)
            {

            }


            return Output.Na;
        }
    }

    public interface IMatch
    {
        bool IsHome { get; }

        int Scored { get; }
        int Missed { get; }

        int Total { get; }
        int Dif { get; }
        

    }

    public enum Output
    {
        Win = 1,
        Lose = 0,
        Deuce = 4,
        Na = -1,
        Nan = -2
    }
}

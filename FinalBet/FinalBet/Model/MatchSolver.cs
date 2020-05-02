using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public static class MatchSolver
    {
        public static Output Solve(IMatch match)
        {
            return Output.Nan;
        }
    }

    public interface IMatch
    {
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

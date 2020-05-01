using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public static class MatchSolver
    {

    }

    public interface IMatch
    {
        int Scored { get; }
        int Missed { get; }

        int Total { get; }
        int Dif { get; }
        
    }

    public enum Outputs
    {
        0 
    }
}

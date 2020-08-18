using System.Collections.Generic;

namespace FinalBet.Model
{
    public interface IMatch
    {
        bool IsEmpty { get; set; }
        bool IsNull { get; set; }

        bool IsHome { get; set; }

        int Scored { get; set; }
        int Missed { get; set; }

        Dictionary<string, double> Odds { get; set; }
    }
}
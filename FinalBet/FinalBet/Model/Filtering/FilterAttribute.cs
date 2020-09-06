using System;

namespace FinalBet.Model.Filtering
{
    public class FilterAttribute : Attribute
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public bool IsHistorical { get; set; }
        public bool NeedSolveMode { get; set; }

    }
}

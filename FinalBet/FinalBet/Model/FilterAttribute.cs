using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public class FilterAttribute : Attribute
    {
        public string Description { get; set; }
        public bool IsHistorical { get; set; }
        public bool NeedSolveMode { get; set; }

    }
}

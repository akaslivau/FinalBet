using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Framework;

namespace FinalBet.Model.Filtering
{
    public class MatchMethod: ViewModelBase
    {
        #region Fields
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsHistorical { get; }
        public bool NeedSolveMode { get; }

        #endregion

        public MatchMethod(int id,string name, string description, bool isHistorical, bool needSolveMode)
        {
            Id = id;
            Name = name;
            Description = description;
            IsHistorical = isHistorical;
            NeedSolveMode = needSolveMode;
        }

        public override string ToString()
        {
            return Description;
        }
    }
}

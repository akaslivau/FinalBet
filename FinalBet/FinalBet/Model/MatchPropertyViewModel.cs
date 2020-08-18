using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Framework;

namespace FinalBet.Model
{
    public class MatchPropertyViewModel: ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        public MatchPropertyViewModel()
        {
            _name = "Test it";
        }
    }
}

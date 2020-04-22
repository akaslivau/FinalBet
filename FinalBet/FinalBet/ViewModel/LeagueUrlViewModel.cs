using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class LeagueUrlViewModel:ViewModelBase
    {
        #region Variables
        private leagueUrl _source;
        public leagueUrl Source
        {
            get
            {
                return _source;
            }
            set
            {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged("Source");
            }
        }

        private string _file = "-";
        public string File
        {
            get
            {
                return _file;
            }
            set
            {
                if (_file == value) return;
                _file = value;
                OnPropertyChanged("File");
            }
        }

        private int _matchesCount=0;
        public int MatchesCount
        {
            get
            {
                return _matchesCount;
            }
            set
            {
                if (_matchesCount == value) return;
                _matchesCount = value;
                OnPropertyChanged("MatchesCount");
            }
        }

        #endregion

        public LeagueUrlViewModel(leagueUrl item)
        {
            Source = item;
        }
    }
}

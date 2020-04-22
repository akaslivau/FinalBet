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

        private int _possibleYear = -1;
        public int PossibleYear
        {
            get
            {
                return _possibleYear;
            }
            set
            {
                if (_possibleYear == value) return;
                _possibleYear = value;
                OnPropertyChanged("PossibleYear");
            }
        }
        #endregion

        public LeagueUrlViewModel(leagueUrl item)
        {
            Source = item;

            //kekeke
            var strYear = item.year;
            if (!string.IsNullOrEmpty(strYear))
            {
                int attempt1;
                if (int.TryParse(strYear, out attempt1))
                {
                    PossibleYear = attempt1;
                }
                else
                {
                    var split = strYear.Substring(0,strYear.IndexOf('/'));
                    if (!string.IsNullOrEmpty(split))
                    {
                        int attempt2;
                        if (int.TryParse(split, out attempt2))
                        {
                            PossibleYear = attempt2;
                        }
                    }
                }
            }
        }
    }
}

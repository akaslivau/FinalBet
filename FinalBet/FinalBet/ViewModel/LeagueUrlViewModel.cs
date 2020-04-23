using System;
using FinalBet.Extensions;
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

        //Это переменная нужна, чтобы загружать матчи для завершенного сезона
        private bool _isCurrent = false;
        public bool IsCurrent
        {
            get
            {
                return _isCurrent;
            }
            set
            {
                if (_isCurrent == value) return;
                _isCurrent = value;
                OnPropertyChanged("IsCurrent");
            }
        }

        public static bool GetIsCurrent(string url)
        {
            //axaxaxaxaxa
            for (int i = 3; i < url.Length; i++)
            {
                var str = url.Substring(i - 3, 4);
                if (!str.IsDigitsOnly()) continue;

                int tst = int.Parse(str);
                if (tst > 1900 && tst < DateTime.Now.Year)
                {
                    return false;
                }
            }
            return true;
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
                    var split = strYear.Substring(0, strYear.IndexOf('/'));
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

            IsCurrent = GetIsCurrent(item.url);
        }
    }
}

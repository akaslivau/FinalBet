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
            get => _source;
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
            get => _file;
            set
            {
                if (_file == value) return;
                _file = value;
                OnPropertyChanged("File");
            }
        }

        private int _matchesCount;
        public int MatchesCount
        {
            get => _matchesCount;
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
            get => _possibleYear;
            set
            {
                if (_possibleYear == value) return;
                _possibleYear = value;
                OnPropertyChanged("PossibleYear");
            }
        }

        //Это переменная нужна, чтобы загружать матчи для завершенного сезона
        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent == value) return;
                _isCurrent = value;
                OnPropertyChanged("IsCurrent");
            }
        }


        #endregion

        #region Static
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

        public static int GetPossibleYear(string strYear)
        {
            if (!string.IsNullOrEmpty(strYear))
            {
                if (int.TryParse(strYear, out int attempt1))
                {
                    return attempt1;
                }

                var split = strYear.Substring(0, strYear.IndexOf('/'));
                if (!string.IsNullOrEmpty(split))
                {
                    if (int.TryParse(split, out int attempt2))
                    {
                        return attempt2;
                    }
                }
            }

            return -1;
        }

        #endregion

        public LeagueUrlViewModel(leagueUrl item)
        {
            Source = item;

            PossibleYear = GetPossibleYear(item.year);
            IsCurrent = GetIsCurrent(item.url);
        }
    }
}

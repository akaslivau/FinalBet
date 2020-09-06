using System.Collections.ObjectModel;
using System.Linq;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class TournamentViewModel:ViewModelBase
    {
        #region Fields
        private ObservableCollection<league> _leagues = new ObservableCollection<league>();
        public ObservableCollection<league> Leagues
        {
            get => _leagues;
            set
            {
                if (_leagues == value) return;
                _leagues = value;
                OnPropertyChanged("Leagues");
            }
        }

        private league _selectedLeague;
        public league SelectedLeague
        {
            get => _selectedLeague;
            set
            {
                if (_selectedLeague == value) return;
                _selectedLeague = value;
                OnPropertyChanged("SelectedLeague");

                InitializeTournaments(value);
            }
        }

        private ObservableCollection<string> _tournaments = new ObservableCollection<string>();
        public ObservableCollection<string> Tournaments
        {
            get => _tournaments;
            set
            {
                if (_tournaments == value) return;
                _tournaments = value;
                OnPropertyChanged("Tournaments");
            }
        }

        private string _selectedTournament;
        public string SelectedTournament
        {
            get => _selectedTournament;
            set
            {
                if (_selectedTournament == value) return;
                _selectedTournament = value;
                OnPropertyChanged("SelectedTournament");

                InitializeYears(SelectedLeague, SelectedTournament);
            }
        }

        private ObservableCollection<leagueUrl> _leagueUrls = new ObservableCollection<leagueUrl>();
        public ObservableCollection<leagueUrl> LeagueUrls
        {
            get => _leagueUrls;
            set
            {
                if (_leagueUrls == value) return;
                _leagueUrls = value;
                OnPropertyChanged("LeagueUrls");
            }
        }

        private leagueUrl _selectedUrl;
        public leagueUrl SelectedUrl
        {
            get => _selectedUrl;
            set
            {
                if (_selectedUrl == value) return;
                _selectedUrl = value;
                OnPropertyChanged("SelectedUrl");

                InitializeSubSeasons(SelectedLeague, value.year);
            }
        }


        private ObservableCollection<matchTag> _subSeasons = new ObservableCollection<matchTag>();
        public ObservableCollection<matchTag> SubSeasons
        {
            get => _subSeasons;
            set
            {
                if (_subSeasons == value) return;
                _subSeasons = value;
                OnPropertyChanged("SubSeasons");
            }
        }

        private matchTag _selectedSubSeason;
        public matchTag SelectedSubSeason
        {
            get => _selectedSubSeason;
            set
            {
                if (_selectedSubSeason == value) return;
                _selectedSubSeason = value;
                OnPropertyChanged("SelectedSubSeason");
            }
        }

        private bool _onlyMainSeason;
        public bool OnlyMainSeason
        {
            get => _onlyMainSeason;
            set
            {
                if (_onlyMainSeason == value) return;
                _onlyMainSeason = value;
                OnPropertyChanged("OnlyMainSeason");
            }
        }


        #endregion

        #region Methods
        private void InitializeLeagues()
        {
            Leagues.Clear();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<league>();

                var items = table.Where(x => x.isFavorite).ToList();

                foreach (var league in items)
                {
                    Leagues.Add(league);
                }

                if (items.Any()) SelectedLeague = Leagues.First();
            }
        }

        private void InitializeTournaments(league league)
        {
            Tournaments.Clear();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == league.id).
                    Select(x => x.mark).
                    Where(x => x.Length > 0).
                    Distinct().
                    ToList();

                foreach (var item in table)
                {
                    Tournaments.Add(item);
                }

                if (Tournaments.Any()) SelectedTournament = Tournaments.First();
            }
        }

        private void InitializeYears(league value, string mark)
        {
            LeagueUrls.Clear();

            if (string.IsNullOrEmpty(SelectedTournament)) return;

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == value.id && x.mark == SelectedTournament).
                    ToList();

                foreach (var item in table)
                {
                    LeagueUrls.Add(item);
                }

                if (LeagueUrls.Any()) SelectedUrl = LeagueUrls.First();
            }
        }

        private void InitializeSubSeasons(league league, string year)
        {
            SubSeasons.Clear();
            if (string.IsNullOrEmpty(year)) return;


            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var parentId = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == league.id).
                    Single(x => x.year == year && x.mark == SelectedTournament)
                    .id;

                var tags = cntx.GetTable<matchTag>().ToList();

                var items = cntx.GetTable<match>().
                    Where(x => x.leagueUrlId == parentId).
                    Select(x => x.tagId).Distinct()
                    .ToList();


                foreach (var item in tags.Where(item => items.Contains(item.id)))
                {
                    SubSeasons.Add(item);
                }

                if (SubSeasons.Any()) SelectedSubSeason = SubSeasons.First();

                OnlyMainSeason = SubSeasons.Count > 1;
            }
        }
        #endregion

        public TournamentViewModel()
        {
            InitializeLeagues();
        }
    }
}

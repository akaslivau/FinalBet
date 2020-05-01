﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FinalBet.Database;
using FinalBet.Framework;
using FinalBet.Properties;

namespace FinalBet.ViewModel
{
    public class RedGreenViewModel:ViewModelBase
    {
        #region Variables

        private ObservableCollection<league> _leagues;
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


        private bool _showOnlyFavorites;
        public bool ShowOnlyFavorites
        {
            get => _showOnlyFavorites;
            set
            {
                if (_showOnlyFavorites == value) return;
                _showOnlyFavorites = value;
                OnPropertyChanged("ShowOnlyFavorites");

                Settings.Default.onlyFavorites = value;
                Settings.Default.Save();

                InitializeLeagues(value);
            }
        }

        private ObservableCollection<string> _tournaments;
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

        private ObservableCollection<string> _leagueYears;
        public ObservableCollection<string> LeagueYears
        {
            get => _leagueYears;
            set
            {
                if (_leagueYears == value) return;
                _leagueYears = value;
                OnPropertyChanged("LeagueYears");
            }
        }

        private string _selectedYear;
        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (_selectedYear == value) return;
                _selectedYear = value;
                OnPropertyChanged("SelectedYear");

                InitializeSubSeasons(SelectedLeague, value);
            }
        }


        private ObservableCollection<matchTag> _subSeasons;
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
        private void InitializeLeagues(bool onlyFavorites)
        {
            Leagues.Clear();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<league>();

                var items = onlyFavorites ? table.Where(x => x.isFavorite).ToList() : table.ToList();

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
                    Where(x=>x.Length>0).
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
            LeagueYears.Clear();

            if(SelectedTournament.Length<1) return;
            
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == value.id && x.mark == SelectedTournament).
                    ToList();

                var items = table.Select(x => x.year).Distinct().ToList();

                foreach (var item in items)
                {
                    LeagueYears.Add(item);
                }

                if (LeagueYears.Any()) SelectedYear = LeagueYears.First();
            }
        }




        private void InitializeSubSeasons(league league, string year)
        {
            SubSeasons.Clear();
            if (string.IsNullOrEmpty(year)) return;


            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var parentId = cntx.GetTable<leagueUrl>().
                    Where(x=>x.parentId == league.id).
                    Single(x => x.year == year && x.mark == SelectedTournament)
                    .id;

                var tags = cntx.GetTable<matchTag>().ToList();

                var items = cntx.GetTable<match>().
                    Where(x => x.parentId == parentId).
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

        public void OnTableMouseClick(object sender, MouseEventArgs eventArgs)
        {
            var canvas = (DrawingCanvas)sender;
            canvas.TrySelectCell(eventArgs);
        }

        #endregion

        public ICommand DrawCommand { get; private set; }

        private void Draw(object prm)
        {
            var matchList = new List<match>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var parentId = cntx.GetTable<leagueUrl>()
                    .Single(x => x.parentId == SelectedLeague.id && x.year == SelectedYear && x.mark == SelectedTournament).id;

                matchList = cntx.GetTable<match>()
                    .Where(x => x.parentId == parentId && x.tagId == SelectedSubSeason.id).ToList();
            }

            if(!matchList.Any()) return;

            var teamNames = Soccer.GetTeamNames(matchList.Select(x => x.homeTeamId).Distinct());

            
            var canvas = (DrawingCanvas) prm;
            canvas.Clear();

            var shift = 4;
            int i = 0;
            //int j = 0;
            var teamCellWidth = DrawingCanvas.GetTeamCellSizeWidth(teamNames.Select(x => x.Value).ToList());
            canvas.TeamCellWidth = teamCellWidth;

            foreach (var teamName in teamNames)
            {
                var teamId = teamName.Key;
                var teamVisual = new DrawingVisual();
                canvas.DrawTeamCell(teamVisual,
                    new Point(shift, shift + i*(canvas.CellSize + shift)), 
                    teamName.Value,
                    new Size(teamCellWidth, canvas.CellSize));

                canvas.AddVisual(teamVisual);

                var line = matchList.Where(x => x.homeTeamId == teamName.Key || x.guestTeamId == teamName.Key).ToList();

                for (int j = 0; j < line.Count; j++)
                {
                    var cellVisual = new DrawingVisual();
                    canvas.DrawCellSquare(
                        cellVisual,
                        new Point(teamCellWidth + canvas.CellSize/4 + j * (canvas.CellSize + shift), shift + i * (canvas.CellSize + shift)),
                        line[j].id.ToString());
                    canvas.AddVisual(cellVisual);
                }

                i++;
            }
        }




        public RedGreenViewModel()
        {
            base.DisplayName = "Красно-зеленая";
            Leagues = new ObservableCollection<league>();
            LeagueYears = new ObservableCollection<string>();
            Tournaments = new ObservableCollection<string>();
            SubSeasons = new ObservableCollection<matchTag>();

            _showOnlyFavorites = Settings.Default.onlyFavorites;

            InitializeLeagues(_showOnlyFavorites);

            DrawCommand = new RelayCommand(Draw, a => SelectedSubSeason != null);
        }
    }
}

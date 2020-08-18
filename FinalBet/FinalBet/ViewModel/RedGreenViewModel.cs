using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FinalBet.Database;
using FinalBet.Extensions;
using FinalBet.Framework;
using FinalBet.Model;
using FinalBet.Properties;

namespace FinalBet.ViewModel
{
    public class RedGreenViewModel:ViewModelBase
    {
        #region Variables
        private SolveMode _solveMode = new SolveMode();
        public SolveMode SolveMode
        {
            get => _solveMode;
            set
            {
                if (_solveMode == value) return;
                _solveMode = value;
                OnPropertyChanged("SolveMode");
            }
        }

        private int _selectedMatchId = -1;
        public int SelectedMatchId
        {
            get => _selectedMatchId;
            set
            {
                if (_selectedMatchId == value) return;
                _selectedMatchId = value;
                OnPropertyChanged("SelectedMatchId");
            }
        }

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

        private MatchPropertyRepoViewModel _filter = new MatchPropertyRepoViewModel();
        public MatchPropertyRepoViewModel Filter
        {
            get => _filter;
            set
            {
                if (_filter == value) return;
                _filter = value;
                OnPropertyChanged("Filter");
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

            if(string.IsNullOrEmpty(SelectedTournament)) return;
            
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

        public void OnTableMouseClick(object sender, MouseEventArgs eventArgs)
        {
            var canvas = (DrawingCanvas)sender;
            canvas.TrySelectCell(eventArgs);

            if (canvas.MatchId != -1)
            {
                SelectedMatchId = canvas.MatchId;
            }
        }

        #endregion

        #region Commands
        public ICommand DrawCommand { get; private set; }

        private void Draw(object prm)
        {
            GetDrawingData(SelectedLeague, SelectedYear, SelectedTournament, SelectedSubSeason, SolveMode,
                out var data
            );

            var teamNamesDict = Soccer.GetTeamNames(data.Select(x => x.Key));

            var shift = 4;

            var canvas = (DrawingCanvas) prm;
            canvas.Clear();
            canvas.TeamCellWidth = DrawingCanvas.GetTeamCellSizeWidth(teamNamesDict.Select(x => x.Value).ToList());
            canvas.Width = 1;
            canvas.Height = data.Count * (canvas.CellSize + shift) + shift;

            int i = 0;
            foreach (var item in data)
            {
                var line = item.Value;
                //Название команды
                var teamVisual = new VisualWithTag();
                canvas.DrawTeamCell(teamVisual,
                    new Point(shift, shift + i * (canvas.CellSize + shift)),
                    teamNamesDict[item.Key],
                    new Size(canvas.TeamCellWidth, canvas.CellSize));

                canvas.AddVisual(teamVisual);

                //Это подборка ширины и высоты канваса
                var cWidth = canvas.TeamCellWidth + canvas.CellSize / 4 + line.Count * (canvas.CellSize + shift);
                if (canvas.Width < cWidth)
                {
                    canvas.Width = cWidth;
                }

                //Отрисовка самих клеток
                for (int j = 0; j < line.Count; j++)
                {
                    var txt = MatchSolver.OutputStrings[line[j].Output];
                    var brush = MatchSolver.OutputBrushes[line[j].Output];
                    var cellVisual = new VisualWithTag {Tag = line[j].MatchId};

                    canvas.DrawCellSquare(
                        cellVisual,
                        new Point(canvas.TeamCellWidth + canvas.CellSize / 4 + j * (canvas.CellSize + shift),
                            shift + i * (canvas.CellSize + shift)),
                        brush,
                        txt);
                    canvas.AddVisual(cellVisual);
                }
                i++;
            }
        }

        private static void GetDrawingData(
            league lg, 
            string year, 
            string tournament, 
            matchTag tag,
            SolveMode mode,
            out Dictionary<int,List<RGmatch>> data
            )
        {
            data = new Dictionary<int, List<RGmatch>>();
            //Получаем список матчей для выбранных уставок
            var matchList = new List<match>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var parentId = cntx.GetTable<leagueUrl>()
                    .Single(x => x.parentId == lg.id && x.year == year && x.mark == tournament).id;

                matchList = cntx.GetTable<match>()
                    .Where(x => x.leagueUrlId == parentId && x.tagId == tag.id).ToList();
            }

            if (!matchList.Any())
            {
                return;
            }

            var teamNames = Soccer.GetTeamNames(matchList);

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var possibleResults = cntx.GetTable<possibleResult>().ToList();
                
                var allIds = matchList.Select(x => x.id);
                //Если режим "Угадывания буком", то подгружаем все ставки для списка матчей
                List<odd> odds = null;
                if (mode.IsBookmakerMode)
                {
                    odds = (from o in cntx.GetTable<odd>()
                            where allIds.Contains(o.parentId) && mode.OddTypes.Contains(o.oddType)
                            select o).ToList();
                }

                foreach (var item in teamNames)
                {
                    //Отбираем матчи для команды с учетом режима [Все матчи, Дома, В гостях]
                    var line = mode.IsHome == null
                        ? matchList.Where(x => x.homeTeamId == item.Key || x.guestTeamId == item.Key)
                            .ToList()
                        : mode.IsHome.Value
                            ? matchList.Where(x => x.homeTeamId == item.Key).ToList()
                            : matchList.Where(x => x.guestTeamId == item.Key).ToList();

                    line = line.OrderBy(x => x.date).ToList(); //Возрастают по дате

                    //Получаем список матчей
                    var rGmatches = (from m in line
                        let resId = mode.MatchPeriod == 0 ? m.matchResultId :mode.MatchPeriod == 1 ? m.firstHalfResId : m.secondHalfResId
                        let matchOdds = (mode.IsBookmakerMode && resId!=null )? odds.Where(x => x.parentId == m.id).ToDictionary(x => x.oddType, x => x.value): null
                        let rgm = new RGmatch(m.homeTeamId == item.Key, resId, possibleResults, matchOdds)
                        {
                            MatchId = m.id,
                        }
                            select rgm
                            ).ToList();

                    var lineOutput = rGmatches.Select(x => MatchSolver.Solve(x, mode)).ToList();
                    rGmatches.ForEach((i, x) => x.Output = lineOutput[i]);
                    data.Add(item.Key, rGmatches);
                }
            }
        }
        
        #endregion

        public RedGreenViewModel()
        {
            base.DisplayName = "Красно-зеленая";
            Leagues = new ObservableCollection<league>();
            LeagueYears = new ObservableCollection<string>();
            Tournaments = new ObservableCollection<string>();
            SubSeasons = new ObservableCollection<matchTag>();

            InitializeLeagues(true);

            DrawCommand = new RelayCommand(Draw, a => SelectedSubSeason != null);
        }


    }
}

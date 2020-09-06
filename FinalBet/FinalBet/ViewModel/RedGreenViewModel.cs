using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Extensions;
using FinalBet.Framework;
using FinalBet.Model;
using FinalBet.Model.Filtering;

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

        private TournamentViewModel _tournament = new TournamentViewModel();
        public TournamentViewModel Tournament
        {
            get => _tournament;
            set
            {
                if (_tournament == value) return;
                _tournament = value;
                OnPropertyChanged("Tournament");
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
        public ICommand DrawCommand { get; }
        public ICommand FilterCommand { get; }

        private void Draw(object prm)
        {
            GetDrawingData(Tournament.SelectedLeague, Tournament.SelectedYear, Tournament.SelectedTournament, Tournament.SelectedSubSeason, SolveMode,
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


        private void RunFilter(object obj)
        {
            
        }

        #endregion

        public RedGreenViewModel()
        {
            base.DisplayName = "Красно-зеленая";
            
            DrawCommand = new RelayCommand(Draw, a =>Tournament.SelectedSubSeason != null);
            FilterCommand = new RelayCommand(RunFilter, a=> Filter.Items.Any());
        }

    }
}

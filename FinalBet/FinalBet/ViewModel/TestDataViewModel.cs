using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class TestDataViewModel: ViewModelBase
    {
        /*  СПИСОК ТЕСТОВ
         * 1. Нет нулевых результатов в match.matchResultId + неНУЛЛевые match.FirstHalfResId.COUNT = match.SecondHalfResId.COUNT
         * 2. Число КОРРЕКТНЫХ результатов с matchPeriod == 0 должно быть равно числу результатов c matchPeriod = 1,2
        */
        #region Async
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged("IsBusy");
            }
        }

        private bool _cancelAsync;
        public bool CancelAsync
        {
            get => _cancelAsync;
            set
            {
                if (_cancelAsync == value) return;
                _cancelAsync = value;
                OnPropertyChanged("CancelAsync");
            }
        }
        public IAsyncCommand TestAllCommand { get; private set; }
        public ICommand BreakCommand
        {
            get
            {
                return new RelayCommand(x =>
                    {
                        CancelAsync = true;
                        IsBusy = false;
                    },
                    a => IsBusy);
            }
        }

        private async Task TestAll()
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagues = cntx.GetTable<league>().Where(x => x.isFavorite);
                Dispatcher.CurrentDispatcher.Invoke(() => { Output = "Тестирую все страны" + "\r\n"; });
                IsBusy = true;

                foreach (var league in leagues)
                {
                    await Task.Run(() =>
                        {
                            var marks = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id)
                                .Where(x => x.mark.Length > 0)
                                .Select(x => x.mark).
                                Distinct().ToList();

                            foreach (var mark in marks)
                            {
                                var testResults = GetTestResults(league, mark);
                                var isOk = testResults.All(x => x);
                                Dispatcher.CurrentDispatcher.Invoke(() =>
                                {
                                    Output += league.name + ".[" + mark + "]: " + (isOk ? "OK" : "ERROR!!!") +
                                              "\r\n";
                                });
                            }
                        }
                    );
                    await Task.Delay(50);
                    if (CancelAsync)
                    {
                        break;
                    }
                }
                IsBusy = false;
                CancelAsync = false;
                Dispatcher.CurrentDispatcher.Invoke(() => { Output += "FINISHED"; });
            }
        }
        #endregion

        #region Fields
        private string _output = "";
        public string Output { get { return _output;} set{ _output = value; OnPropertyChanged("Output");}}

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
                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var resultBorder = cntx.GetTable<border>().SingleOrDefault(x =>
                        x.leagueId == SelectedLeague.id && x.mark == SelectedTournament);

                    _resultBorderYear = resultBorder?.resultBorderYear ?? -1;
                    OnPropertyChanged("ResultBorderYear");
                }
            }
        }

        private int _resultBorderYear = -1;

        public int ResultBorderYear
        {
            get => _resultBorderYear;
            set
            {
                _resultBorderYear = value;
                OnPropertyChanged("ResultBorderYear");
                UpdateResultBorderYear(SelectedLeague, SelectedTournament, value);
            }
        }

        private static void UpdateResultBorderYear(league league, string mark, int newValue)
        {
            if(league.id <=0 || string.IsNullOrEmpty(mark)) return;

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<border>();
                if (!table.Any(x => x.leagueId == league.id && x.mark == mark))
                {
                    var toAdd = new border
                    {
                        leagueId = league.id,
                        mark = mark,
                        resultBorderYear =  -1,
                        oddBorderYear = -1
                    };
                    table.InsertOnSubmit(toAdd);
                    cntx.SubmitChanges();
                }

                var sngl = table.Single(x => x.leagueId == league.id && x.mark == mark);
                sngl.resultBorderYear = newValue;
                cntx.SubmitChanges();
            }
        }


        #endregion

        #region Commands
        public ICommand DoTestCommand { get; private set; }

        public ICommand TestTwoCommand { get; private set; }
        public ICommand ClearHalfResultsCommand { get; private set; }
        
        /// <summary>
        /// Запуск тестов целостности базы данных для выбранной страны
        /// </summary>
        /// <param name="obj">Not used</param>
        private void DoTest(object obj)
        {
            Output = "Результаты выполнения тестов для [" + SelectedLeague.name.ToUpper() + "]\r\n";

            var testResults = GetTestResults(SelectedLeague, SelectedTournament);

            Output += "\n" + (testResults.All(x => x) ? "Все тесты пройдены успешно" : "Обнаружены ошибки") + "\n";

            var lines = testResults.Select((x, i) => (x ? "OK." : "ОШИБКА!") + " Тест №" + (i + 1)).ToList();
            Output += string.Join("\n", lines);
        }



        private void TestTwo(object obj)
        {
            Output = "Результаты выполнения теста №3 для [" 
                     + SelectedLeague.name.ToUpper() + "]." 
                     + "["+SelectedTournament+"]\r\n";

            var test2 = Test2(SelectedLeague, SelectedTournament, out var output);

            if (test2)
            {
                Output += "\n" + "Успешно";
                return;
            }

            Output += "\n" + "Обнаружены следующие ошибки\n\n";
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urlTable = cntx.GetTable<leagueUrl>();
                var ids = output.Select(x => x.Key).ToList();
                var items = urlTable.Where(x => ids.Contains(x.id)).
                    Select(x => new
                    {
                        Id = x.id.ToString(),
                        Name = x.name,
                        Year = x.year,
                        Output = output[x.id]
                    }).
                    Select(x=>string.Join("\t\t", new string[]{x.Id, x.Name, x.Year, x.Output})).
                    ToList();

                Output += string.Join("\n", items);
            }
        }

        private void ClearHalfResultUnderBorder(object obj)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urls = cntx.GetTable<leagueUrl>().Where(x => x.parentId == SelectedLeague.id)
                    .Where(x => x.mark == SelectedTournament).ToList();

                var urlIds = urls.Where(x=>LeagueUrlViewModel.GetPossibleYear(x.year)<ResultBorderYear).
                    Select(x => x.id).ToList();

                var matches = cntx.GetTable<match>().Where(x => x.leagueId == SelectedLeague.id)
                    .Where(x => urlIds.Contains(x.leagueUrlId)).ToList();

                matches.ForEach(x =>
                {
                    x.firstHalfResId = null;
                    x.secondHalfResId = null;
                });
                cntx.SubmitChanges();
            }
        }

        private static bool Test1(league league)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var query = from match in cntx.GetTable<match>()
                    where match.leagueId == league.id
                    group match by match.leagueUrlId
                    into g
                    let firstHalf = g.Count(x => x.firstHalfResId != null)
                    let secondHalf = g.Count(x => x.secondHalfResId != null)
                    select firstHalf == secondHalf;

                return query.All(x => x);
            }
        }

        private static bool Test2(league league, string mark, out Dictionary<int,string> output)
        {
            var res = true;
            output = new Dictionary<int, string>();

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urlTable = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id && x.mark == mark)
                    .Select(x => x.id).ToList();
                var query = (from match in cntx.GetTable<match>().Where(x => urlTable.Contains(x.leagueUrlId))
                    group match by match.leagueUrlId
                    into g
                    select new
                    {
                        UrlId = g.Key,
                        FullTime = g.Select(x => x.matchResultId).ToList(),
                        FirstHalf = g.Select(x => x.firstHalfResId).ToList(),
                        SecondHalf = g.Select(x => x.secondHalfResId).ToList()
                    }).ToList();

                var possibleResult = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, x => x.isCorrect);
                foreach (var item in query)
                {
                    var fullTimeCount = item.FullTime.Count(x=>possibleResult[x]);
                    var firstHalfCount = item.FirstHalf.Where(x=>x.HasValue).Count(x => possibleResult[x.Value]);
                    var secondHalfCount = item.SecondHalf.Where(x => x.HasValue).Count(x => possibleResult[x.Value]);

                    var notOk = (fullTimeCount != firstHalfCount && firstHalfCount!=0) || 
                                (fullTimeCount != secondHalfCount && secondHalfCount!=0);
                    if (notOk)
                    {
                        res = false;
                    }

                    output.Add(item.UrlId, "Total: " + fullTimeCount 
                                                         + "\t\tFirst: " + firstHalfCount
                                                         + "\t\tSecond: " + secondHalfCount
                                                         + "\t\tDifference: " + (fullTimeCount-secondHalfCount)
                                                         + "\t\t"+(notOk ? "WARNING" : "OK... ")
                                                         );
                    
                }
            }

            return res;
        }

        private List<bool> GetTestResults(league league, string mark)
        {
            return new List<bool>
            {
                Test1(league),
                Test2(league, mark, out _)
            };
        }
        #endregion

        public TestDataViewModel()
        {
            base.DisplayName = "Тесты БД";
            Leagues = new ObservableCollection<league>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<league>().Where(x => x.isFavorite);
                foreach (var league in table)
                {
                    Leagues.Add(league);
                }

                if (Leagues.Any()) SelectedLeague = Leagues.First();
            }

            DoTestCommand = new RelayCommand(DoTest, a => SelectedLeague != null);
            TestAllCommand = new AsyncCommand(TestAll);
            TestTwoCommand = new RelayCommand(TestTwo, a => SelectedLeague != null);
            ClearHalfResultsCommand = new RelayCommand(ClearHalfResultUnderBorder, a=> SelectedLeague!=null);
        }
    }
}

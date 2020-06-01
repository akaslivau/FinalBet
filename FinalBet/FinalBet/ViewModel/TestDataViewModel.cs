using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using FinalBet.Database;
using FinalBet.Framework;
using FinalBet.Properties;
using FinalBet.Model;

namespace FinalBet.ViewModel
{
    public class TestDataViewModel: ViewModelBase
    {
        /*  СПИСОК ТЕСТОВ
         * 1. Количество результатов для matchPeriod = 1 AND matchPeriod = 2 должно быть одинаковым
         * 2. [Сомнительно] Все results для matchPeriod = 1,2 должны быть isCorrect
         * 3. Число КОРРЕКТНЫХ результатов с matchPeriod == 0 должно быть равно числу результатов c matchPeriod = 1,2
        */


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
        public IAsyncCommand TestAllCommand { get; private set; }
        public ICommand TestThreeCommand { get; private set; }



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

        private async Task TestAll()
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagues = cntx.GetTable<league>().Where(x => x.isFavorite);
                Dispatcher.CurrentDispatcher.Invoke(() => { Output = "Тестирую все страны" + "\r\n"; });

                foreach (var league in leagues)
                {
/*                    await Task.Run(() =>
                        {
                            var testResults = GetTestResults(league);
                            var isOk = testResults.All(x => x);
                            Dispatcher.CurrentDispatcher.Invoke(() =>
                            {
                                Output += league.name + ": " + (isOk ? "OK" : "ERROR!!!") + "\r\n";
                            });
                        }
                    );*/
                    await Task.Delay(50);
                }
                Dispatcher.CurrentDispatcher.Invoke(() => { Output += "FINISHED"; });
            }
        }

        private void TestThree(object obj)
        {
            Output = "Результаты выполнения теста №3 для [" 
                     + SelectedLeague.name.ToUpper() + "]." 
                     + "["+SelectedTournament+"]\r\n";

            var test3 = Test3(SelectedLeague, SelectedTournament, out var output);

            if (test3)
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

        private static bool Test1(league league, string mark)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urlTable = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id && x.mark == mark);

                var matchTable = cntx.GetTable<match>();
                var resultTable = cntx.GetTable<result>();

                var query1 = (from result in resultTable
                    where result.matchPeriod == 1
                    from url in urlTable
                    where (from match in matchTable
                        where match.parentId == url.id
                        select match.id).Contains(result.parentId)
                    group result by url.id
                    into g
                    select g.Count()).ToList();

                var query2 = (from result in resultTable
                    where result.matchPeriod == 2
                    from url in urlTable
                    where (from match in matchTable
                        where match.parentId == url.id
                        select match.id).Contains(result.parentId)
                    group result by url.id
                    into g
                    select g.Count()).ToList();

                return (query1.Zip(query2, (a, b) => a == b)).All(x => x);
            }
        }

        private static bool Test2(league league, string mark)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagueUrlIds = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == league.id && x.mark == mark).
                    Select(x => x.id)
                    .ToList();

                var matchTable = cntx.GetTable<match>();
                var resultTable = cntx.GetTable<result>();

                var possibleResults = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, x => x.isCorrect);

                var firstHalfRes = (from result in resultTable
                    where (from match in matchTable
                        where leagueUrlIds.Contains(match.parentId)
                        select match.id).Contains(result.parentId)
                    where result.matchPeriod == 1
                    orderby result.id
                    select result.resultId).ToList();

                var secondHalfRes = (from result in resultTable
                    where (from match in matchTable
                        where leagueUrlIds.Contains(match.parentId)
                        select match.id).Contains(result.parentId)
                    where result.matchPeriod == 1
                    orderby result.id
                    select result.resultId).ToList();

                return firstHalfRes.Zip(secondHalfRes, 
                        (a, b) => possibleResults[a] && possibleResults[b])
                    .All(x => x);
            }
        }

        private static bool Test3(league league, string mark, out Dictionary<int,string> output)
        {
            var res = true;
            output = new Dictionary<int, string>();

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var resultTable = cntx.GetTable<result>();
                var matchTable = cntx.GetTable<match>();
                var urlTable = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id && x.mark == mark);

                var list = (from result in resultTable
                    from url in urlTable
                    where (from match in matchTable
                        where match.parentId == url.id
                        select match.id).Contains(result.parentId)
                    let test = url
                    group result by url.id
                    into g
                    select new {UrlId = g.Key, Results = g.ToList()}).ToList();

                var possibleResult = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, x => x.isCorrect);
                foreach (var item in list)
                {
                    var fullTimeCount = item.Results.Where(x => x.matchPeriod == 0).
                        Count(x=>possibleResult[x.resultId]);
                    var firstHalfCount = item.Results.Where(x => x.matchPeriod == 1).Count(x => possibleResult[x.resultId]);
                    var secondHalfCount = item.Results.Where(x => x.matchPeriod == 2).Count(x => possibleResult[x.resultId]);

                    var notOk = fullTimeCount != firstHalfCount || fullTimeCount != secondHalfCount;
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
                Test1(league, mark),
                Test2(league, mark),
                Test3(league, mark, out _)
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
            TestThreeCommand = new RelayCommand(TestThree, a => SelectedLeague != null);
        }
    }
}

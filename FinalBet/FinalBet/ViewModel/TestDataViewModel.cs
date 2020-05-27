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
         * 3. 
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
            }
        }
        
        #endregion

        #region Commands
        public ICommand DoTestCommand { get; private set; }
        public IAsyncCommand TestAllCommand { get; private set; }



        /// <summary>
        /// Запуск тестов целостности базы данных для выбранной страны
        /// </summary>
        /// <param name="obj">Not used</param>
        private void DoTest(object obj)
        {
            Output = "Результаты выполнения тестов для [" + SelectedLeague.name.ToUpper() + "]\r\n";

            var testResults = GetTestResults(SelectedLeague);

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
                    await Task.Run(() =>
                        {
                            var testResults = GetTestResults(league);
                            var isOk = testResults.All(x => x);
                            Dispatcher.CurrentDispatcher.Invoke(() =>
                            {
                                Output += league.name + ": " + (isOk ? "OK" : "ERROR!!!") + "\r\n";
                            });
                        }
                    );
                    await Task.Delay(50);
                }
                Dispatcher.CurrentDispatcher.Invoke(() => { Output += "FINISHED"; });
            }
        }

        private static bool Test1(league league)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagueId= cntx.GetTable<league>().Single(x => x.id == league.id).id;
                var leagueUrlIds = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == leagueId).
                    Select(x => x.id)
                    .ToList();

                var matchTable = cntx.GetTable<match>();
                var resultTable = cntx.GetTable<result>();

                var results = from result in resultTable
                              where (from match in matchTable
                                     where leagueUrlIds.Contains(match.parentId)
                                     select match.id).Contains(result.parentId)
                              select result;

                return results.Count(x => x.matchPeriod == 1) == results.Count(x => x.matchPeriod == 2);
            }
        }

        private static bool Test2(league league)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagueId = cntx.GetTable<league>().Single(x => x.id == league.id).id;
                var leagueUrlIds = cntx.GetTable<leagueUrl>().
                    Where(x => x.parentId == leagueId).
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

                return firstHalfRes.Zip(secondHalfRes, (a, b) => possibleResults[a] && possibleResults[b])
                    .All(x => x);
            }
        }

        private List<bool> GetTestResults(league league)
        {
            return new List<bool>
            {
                Test1(league),
                Test2(league)
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
        }
    }
}

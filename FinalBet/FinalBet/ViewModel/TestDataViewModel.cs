using System;
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
    /// <summary>
    /// Проведение тестов базы данных на предмет целостности данных
    /// </summary>
    public class TestDataViewModel: ViewModelBase
    {
        /*  СПИСОК ТЕСТОВ
         * 1. match.FirstHalfResId.COUNT = match.SecondHalfResId.COUNT
         * 2. Число КОРРЕКТНЫХ результатов с matchPeriod == 0 должно быть равно числу неНУЛЛЕвых результатов c matchPeriod = 1,2
         *  Либо все результаты c matchPeriod = 1,2 должны быть равны NULL.
         * 3. Для всех notCorrect match.MatchResultId matchPeriod = 1,2 должны быть равны NULL (иначе требуется запуск Coerce)
         * 4. Для всех Correct match.MatchResultId, MATCH_SCORED = FirstHalfScored + SecondHalfScored (то же для Missed)
         * 5. PossibleResults NOT_CORRECT не должны содержать полей, НЕ равных -1. Соответственно не должно быть матчей со ссылкой на них
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
        public Dictionary<int, string> TestDescriptions = new Dictionary<int, string>
        {
            { 0, "Not NULL match.FirstHalfResId.COUNT = match.SecondHalfResId.COUNT" },
            { 1, "ForEach LeagueUrl CorrectFinalResults.Count == CorrectFirstOrSecondHalfResult.Count" },
            { 2, "ForEach NOTcorrect match.FinalResult FirstAndSecondHalfResult = null" },
            { 3, "ForEach Correct MATCH_SCORED = FirstHalfScored + SecondHalfScored (MISSED THE SAME)" },
            { 4, "TESTING POSSIBLE RESULTS" }
        };


        private string _output = "";
        public string Output { get { return _output;} set{ _output = value; OnPropertyChanged("Output");}}

        private bool _coerce4;
        public bool Coerce4
        {
            get
            {
                return _coerce4;
            }
            set
            {
                if (_coerce4 == value) return;
                _coerce4 = value;
                OnPropertyChanged("Coerce4");
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
        public ICommand ClearHalfResultsCommand { get; private set; }


        public ICommand TestOneCommand { get; private set; }
        public ICommand TestTwoCommand { get; private set; }
        public ICommand TestThreeCommand { get; private set; }
        public ICommand TestFourCommand { get; private set; }
        public ICommand TestFiveCommand { get; private set; }


        /// <summary>
        /// Запуск тестов целостности базы данных для выбранной страны
        /// </summary>
        /// <param name="obj">Not used</param>
        private void DoTest(object obj)
        {
            Output = "Результаты выполнения тестов для [" + SelectedLeague.name.ToUpper() + "]\r\n";

            var testResults = GetTestResults(SelectedLeague, SelectedTournament);

            Output += "\n" + (testResults.All(x => x) ? "Все тесты пройдены успешно" : "Обнаружены ошибки") + "\n";

            var lines = testResults.Select(
                (x, i) => 
                    (x ? "OK..." : "ERROR") + "\t\tТест №" + (i + 1) + "\t\t" + TestDescriptions[i]).ToList();
            Output += string.Join("\n", lines);
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

        private void TestOne(object obj)
        {
            Output = (Test1(SelectedLeague) ? "OK" : "Error") + "\t\tТест №1" + "\t\t" + TestDescriptions[0];
        }

        private void TestTwo(object obj)
        {
            Output = "Результаты выполнения теста №3 для ["
                     + SelectedLeague.name.ToUpper() + "]."
                     + "[" + SelectedTournament + "]\r\n";

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
                    Select(x => string.Join("\t\t", new string[] { x.Id, x.Name, x.Year, x.Output })).
                    ToList();

                Output += string.Join("\n", items);
            }
        }

        private void TestThree(object obj)
        {
            Output = (Test3(SelectedLeague, SelectedTournament, out var errors) ? "OK" : "Error") + "\t\tТест №3" + "\t\t" + TestDescriptions[2];
            Output += "\n" + errors;
        }
        private void TestFour(object obj)
        {
            Output = (Test4(SelectedLeague, SelectedTournament, out var errors, Coerce4) ? "OK" : "Error") + "\t\tТест №4" + "\t\t" + TestDescriptions[3];
            Output += "\n" + errors;
        }

        private void TestFive(object obj)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var badPossible = cntx.GetTable<possibleResult>().Where(x => !x.isCorrect)
                    .Where(x => x.scored != -1 || x.missed != -1 || x.total != -1 || x.diff != -1).Select(x => x.id)
                    .ToList();

                var matches = cntx.GetTable<match>().Where(x => badPossible.Contains(x.matchResultId)).ToList();
                var isOk = !matches.Any();
                Output = (isOk ? "OK" : "Error") + "\t\tТест №5" + "\t\t" + TestDescriptions[4];
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

        private static bool Test3(league league, string mark, out string output, bool coerce = false)
        {
            output = "";
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urlTable = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id && x.mark == mark)
                    .Select(x => x.id).ToList();

                var notCorrectIds = cntx.GetTable<possibleResult>().Where(x => !x.isCorrect).Select(x => x.id).ToList();

                var query = (from match in cntx.GetTable<match>().Where(x => urlTable.Contains(x.leagueUrlId))
                                        where notCorrectIds.Contains(match.matchResultId)
                                        select match).ToList();

                var isOk = query.All(x => x.firstHalfResId == null && x.secondHalfResId == null);
                if (isOk) return true;

                var dict = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, y => y);
                var ids = query.Where(x => x.firstHalfResId != null || x.secondHalfResId != null).Select(x => x.id).ToList();

                var teamNames = cntx.GetTable<teamName>();

                var forOutput = from match in cntx.GetTable<match>()
                    where ids.Contains(match.id)
                    select match;

                foreach (var match in forOutput)
                {
                    var lst = new List<string>
                    {
                        match.date.ToShortDateString(),
                        teamNames.Single(x=>x.id == match.homeTeamId).name,
                        teamNames.Single(x=>x.id == match.guestTeamId).name,
                        dict[match.matchResultId].value,
                        match.firstHalfResId.HasValue? dict[match.firstHalfResId.Value].value: "NULL",
                        match.secondHalfResId.HasValue? dict[match.secondHalfResId.Value].value: "NULL"
                    };

                    output += "\n" + string.Join("\t", lst);
                }
                return false;
            }
        }

        private static bool Test4(league league, string mark, out string output, bool coerce = false)
        {
            output = "";
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var urlTable = cntx.GetTable<leagueUrl>().Where(x => x.parentId == league.id && x.mark == mark)
                    .Select(x => x.id).ToList();

                var correctIds = cntx.GetTable<possibleResult>().Where(x => x.isCorrect).Select(x => x.id).ToList();

                var query = (from match in cntx.GetTable<match>().Where(x => urlTable.Contains(x.leagueUrlId))
                    where correctIds.Contains(match.matchResultId)
                    where match.firstHalfResId != null
                    select match).ToList();

                var dict = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, y => y);

                var res = (from match in query
                    let final = dict[match.matchResultId]
                    let firstHalf = dict[match.firstHalfResId.Value]
                    let secondHalf = dict[match.secondHalfResId.Value]
                    let scd = final.scored - firstHalf.scored - secondHalf.scored
                    let msd = final.missed - firstHalf.missed - secondHalf.missed
                    select new {MatchId=match.id, IsOk = scd == 0 && msd == 0}).ToList();

                if (res.All(x => x.IsOk)) return true;

                var ids = res.Where(x => !x.IsOk).Select(x => x.MatchId).ToList();

                var teamNames = cntx.GetTable<teamName>();

                var forOutput = from match in cntx.GetTable<match>()
                    where ids.Contains(match.id)
                    select match;

                foreach (var match in forOutput)
                {
                    var lst = new List<string>
                    {
                        match.date.ToShortDateString(),
                        teamNames.Single(x=>x.id == match.homeTeamId).name,
                        teamNames.Single(x=>x.id == match.guestTeamId).name,
                        dict[match.matchResultId].value,
                        dict[match.firstHalfResId.Value].value,
                        dict[match.secondHalfResId.Value].value
                    };

                    output += "\n" + string.Join("\t", lst);
                }

                if (coerce)
                {
                    var possibleResults = cntx.GetTable<possibleResult>();
                    foreach (var match in forOutput)
                    {
                        var possibleResult = possibleResults.Single(x => x.id == match.matchResultId);

                        var psbl1 = possibleResults.Single(x => x.id == match.firstHalfResId);
                        var psbl2 = possibleResults.Single(x => x.id == match.secondHalfResId);

                        var psbl = possibleResults.Where(x => x.scored == (psbl1.scored + psbl2.scored) &&
                                                              x.missed == (psbl1.missed + psbl2.missed)
                        ).ToList();

                        if (psbl.Count > 1) throw new Exception();

                        if (psbl.Count == 0)
                        {
                            var scored = psbl1.scored + psbl2.scored;
                            var missed = psbl1.missed + psbl2.missed;
                            var toAdd = new possibleResult()
                            {
                                scored = scored,
                                missed = missed,
                                isCorrect = true,
                                value = scored + BetExplorerParser.BE_SCORE_DELIMITER + missed.ToString(),
                                total = scored + missed,
                                diff = scored - missed
                            };
                            possibleResults.InsertOnSubmit(toAdd);
                            cntx.SubmitChanges();
                        }

                        var newPsbl = possibleResults.Single(x => x.scored == (psbl1.scored + psbl2.scored) &&
                                                              x.missed == (psbl1.missed + psbl2.missed));

                        var toUpdate = cntx.GetTable<match>().Single(x => x.id == match.id);
                        toUpdate.matchResultId = newPsbl.id;
                        cntx.SubmitChanges();
                    }
                }

                return false;
            }
        }

        private List<bool> GetTestResults(league league, string mark)
        {
            return new List<bool>
            {
                Test1(league),
                Test2(league, mark, out _),
                Test3(league, mark, out _),
                Test4(league, mark, out _)
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
            ClearHalfResultsCommand = new RelayCommand(ClearHalfResultUnderBorder, a=> SelectedLeague!=null);

            TestOneCommand = new RelayCommand(TestOne, a => SelectedLeague != null);
            TestTwoCommand = new RelayCommand(TestTwo, a => SelectedLeague != null);
            TestThreeCommand = new RelayCommand(TestThree, a => SelectedLeague != null);
            TestFourCommand = new RelayCommand(TestFour, a => SelectedLeague != null);
            TestFiveCommand = new RelayCommand(TestFive);
        }
    }
}

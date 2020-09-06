using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Extensions;
using FinalBet.Framework;
using FinalBet.Model;
using FinalBet.Properties;
using Serilog;
// ReSharper disable All

namespace FinalBet.ViewModel
{
    public class DatabaseViewModel: ViewModelBase
    {
        /// <summary>
        /// Общая логика работы данного раздела.
        /// 1. Список всех стран загружается вручную единожды BetExplorerParser.ParseSoccerPage()
        /// 2. Затем загружаются ссылки для каждой страны. Либо отдельно, либо массово для всех.
        /// 3. Для того, чтобы загрузить матчи для ссылки ее необходимо пометить - основной турнир, кубок и т.д.
        /// Загрузка матчей возможна только для завершенного турнира
        /// Можно загрузить матчи для
        /// - выбранной ссылки
        /// - все отмеченные ссылки для выбранной страны
        /// - вообще все отмеченные ссылки
        /// HTML для загруженных ссылок попадает в архив
        /// 4. Для матчей загружаются счета таймов.
        /// При этом желательно выставить отсечку по годам для турнира, так как ниже 2010 года счетов таймов почти нет
        /// 5. Загружаются коэффициенты
        /// 
        /// </summary>

        public DatabaseViewModel()
        {
            //TODO: Тесты для кэфов
            //TODO: оперативное добавление лиги
            //TODO: загрузка счетов таймов для выбранной ссылки-лиги-турнира в лиге
            //TODO: список методов-свойств
            //TODO: фильтр по методам-свойствам
            //TODO: После загрузки фор, и тоталов провести массовую аппроксимацию для них. В случае отсутствия коэффициентов заполнить таблицы аппроксимированными значениями

            base.DisplayName = "База данных";

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<league>().ToList();
                foreach (var league in table)
                {
                    Items.Add(league);
                }
            }
            Table = new ListCollectionView(Items);
            if (Items.Any()) Selected = Items[0];

            ShowOnlyFavorites = Settings.Default.onlyFavorites;
            if (ShowOnlyFavorites)
            {
                Table.Filter = FilterLeagues;
                Table.Refresh();
                Selected = (league)Table.GetItemAt(0);
            }

            TestAsyncCommand = new AsyncCommand(TestAsyncTask);
            TestCommand = new RelayCommand(Test);

            SetUrlsRepoCommand = new AsyncCommand(SetUrlsRepo);
            //
            LoadCountryUrlsCommand = new AsyncCommand(() => LoadCountryUrls(Selected), () => Selected != null);
            LoadAllUrlsCommand = new AsyncCommand(LoadAllUrls, () => Items.Any());
            //
            LoadMatchesCommand = new AsyncCommand(() => LoadMatches(),() => Selected != null && SelectedMatchLoadMode != null);
            //
            LoadMatchDetailsCommand = new AsyncCommand(LoadMatchDetails);
            CoerceResultsCommand = new RelayCommand(CoerceResults);
            //
            Load1x2CoefsCommand = new AsyncCommand(Load1x2Coefs);
            LoadOuCoefsCommand = new AsyncCommand(() => LoadCoefs(BeOddLoadMode.OU));
            LoadForaCoefsCommand = new AsyncCommand(() => LoadCoefs(BeOddLoadMode.AH));
            LoadBtsCoefsCommand = new AsyncCommand(() => LoadCoefs(BeOddLoadMode.BTS));
            //
            ShowFileDetailsCommand = new RelayCommand(ShowFileDetails, a => LeagueUrls.Items.Any());
            //
            MarkSelectedUrlsCommand = new RelayCommand(x => MarkSelectedUrls(x, SelectedLeagueMark.name),
                a => LeagueUrls.Selected != null && SelectedLeagueMark != null);
            UnmarkSelectedUrlsCommand = new RelayCommand(x => MarkSelectedUrls(x, ""), a => LeagueUrls.Selected != null);
            MarkAutoCommand = new RelayCommand(MarkAuto, a => LeagueUrls.Items.Any() && SelectedLeagueMark != null);
            CheckMarksCommand = new RelayCommand(CheckMarks);
            //
            RemoveLeagueDataCommand = new RelayCommand(RemoveLeagueData, x => Selected != null && SelectedLeagueMark != null);

        }

        #region CommonVariables
        private bool _isBusy;
        public bool IsBusy //Выполняется ли асинхронная задача
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
        public bool CancelAsync //Отменить ли асхинхронную задачу?
        {
            get => _cancelAsync;
            set
            {
                if (_cancelAsync == value) return;
                _cancelAsync = value;
                OnPropertyChanged("CancelAsync");
            }
        }

        private double _pBarValue;
        public double ProgressBarValue //Значение прогресс бара при асинхронной задаче
        {
            get => _pBarValue;
            set
            {
                if (Math.Abs(_pBarValue - value) < 0.0001) return;
                _pBarValue = value;
                OnPropertyChanged("ProgressBarValue");
            }
        }

        public ICommand BreakCommand //Прерывание асинхронной задачи
        {
            get
            {
                return new RelayCommand(x =>
                    {
                        CancelAsync = true;
                        StatusText = "Операция прервана";
                        Log.Information("Прерывание асинхронной операции");
                        Global.Current.Infos++;
                        IsBusy = false;
                    },
                    a => IsBusy);
            }
        }
        #endregion
        
        #region Variables
        //Отрисовка флага из /Images/Flags
        private string _flagPath;
        public string FlagPath
        {
            get => _flagPath;
            set
            {
                if (_flagPath == value) return;
                _flagPath = value;
                OnPropertyChanged("FlagPath");
            }
        }

        //Список стран [CollectionView]
        private ListCollectionView _table;
        public ListCollectionView Table
        {
            get => _table;
            set
            {
                if (_table == value) return;
                _table = value;
                OnPropertyChanged("Table");
            }
        }

        //Список стран
        private ObservableCollection<league> _items = new ObservableCollection<league>();
        public ObservableCollection<league> Items
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                OnPropertyChanged("Items");
            }
        }

        //Выбранная страна
        private league _selected;
        public league Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged("Selected");
            }
        }
        
        private bool _showOnlyFavorites;//Звездочка у стран и ее фильтр
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

                if (value) Table.Filter = FilterLeagues;
                else
                {
                    Table.Filter = null;
                }
                Table.Refresh();
            }
        }

        private bool FilterLeagues(object a)
        {
            var ctr = (league)a;
            return ctr.isFavorite;

        }

        //Список доступных ссылок
        private LeagueUrlRepoViewModel _leagueUrls = new LeagueUrlRepoViewModel();
        public LeagueUrlRepoViewModel LeagueUrls
        {
            get => _leagueUrls;
            set
            {
                if (_leagueUrls == value) return;
                _leagueUrls = value;
                OnPropertyChanged("LeagueUrls");
            }
        }
        
        private bool _isFavorite; //Звездочка у лиги рядом с флагом
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value) return;
                _isFavorite = value;

                Selected.isFavorite = value;
                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var table = cntx.GetTable<league>();
                    var single = table.Single(x => x.id == Selected.id);

                    single.isFavorite = value;
                    cntx.SubmitChanges();
                }

                if (ShowOnlyFavorites)
                {
                    Table.Refresh();
                }

                OnPropertyChanged("IsFavorite");
            }
        }

        //Режим загрузки матчей
        private string _selectedMatchLoadMode = Global.MatchLoadModes[0];
        public string SelectedMatchLoadMode { get { return _selectedMatchLoadMode;} set{ _selectedMatchLoadMode = value; OnPropertyChanged("SelectedMatchLoadMode");}}
        
        //Отметки Основной турнир, вторая лига и т.д.
        private leagueMark _selectedLeagueMark = Global.LeagueMarks[0];
        public leagueMark SelectedLeagueMark
        {
            get => _selectedLeagueMark;
            set
            {
                if (_selectedLeagueMark == value) return;
                _selectedLeagueMark = value;
                OnPropertyChanged("SelectedLeagueMark");
            }
        }

        //Статус...
        private string _statusText="";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged("StatusText");
            }
        }
        #endregion

        #region Ссылки
        public IAsyncCommand LoadCountryUrlsCommand { get; private set; }
        public IAsyncCommand LoadAllUrlsCommand { get; private set; }
        public ICommand ShowFileDetailsCommand { get; private set; }

        //Для выбранной страны. Загружает ссылки с сайта и добавляет их в БД, если она пуста
        private async Task LoadCountryUrls(league ctr)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                bool anyItemExists = table.Any(x => x.parentId == ctr.id);

                var country = ctr.name;
                //TODO: Нужно как-то определять действительно новые URL и добавлять их
                if (anyItemExists)
                {
                    Log.Warning("LoadUrls, table not empty! {@country}", country);
                    Global.Current.Warnings++;
                    return;
                }

                try
                {
                    IsBusy = true;
                    StatusText = "Начинаю загрузку ссылок для " + ctr.name;

                    var htmlToParse = await BetExplorerParser.GetLeagueUrlsHtml(ctr);
                    var parsedUrls = BetExplorerParser.GetLeagueUrls(htmlToParse, ctr.id);
                    
                    table.InsertAllOnSubmit(parsedUrls);
                    cntx.SubmitChanges();

                    StatusText = "Успешно!";
                    Log.Information("LeagueUrls загружены для страны {@country}", country);
                    Global.Current.Infos++;
                }
                catch (Exception ex)
                {
                    StatusText = "Возникло исключение, смотри логи!";
                    Log.Warning(ex, "Task LoadUrls()");
                    Global.Current.Warnings++;
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        //Загрузка ссылок для всех стран с сайта и добавление их в БД (если она пуста)
        private async Task LoadAllUrls()
        {
            try
            {
                IsBusy = true;
                var total = Items.Count;
                int i = 0;
                foreach (var country in Items)
                {
                    if (CancelAsync) break;
                    await LoadCountryUrls(country);

                    i++;
                    ProgressBarValue = 100 * ((double)i / (double)total);
                }
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }

        private void ShowFileDetails(object a)
        {
            using (var cntx = new SqlDataContext(Connection.Con))
            {
                var matches = cntx.GetTable<match>();
                foreach (var item in LeagueUrls.Items)
                {
                    var zipPath = BetExplorerParser.GetZipPath(Selected, item.Source);
                    item.File = File.Exists(zipPath) ? Path.GetFileName(zipPath) : "-";
                    item.MatchesCount = matches.Count(x => x.leagueUrlId == item.Source.id);
                }
            }
        }

        #endregion

        #region Матчи
        public IAsyncCommand LoadMatchesCommand { get; private set; }

        public ICommand OpenArchiveFolderCommand
        {
            get
            {
                return new RelayCommand((o => Process.Start(Settings.Default.zipFolder)), x => Directory.Exists(Settings.Default.zipFolder));
            }
        }

        private async Task LoadMatches()
        {
            var data = GetDataForOperations();
            await LoadBatchMatches(data);
        }
        
        /// <summary>
        /// Выполняет LoadUrlMatches для выбранной коллекции
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task LoadBatchMatches(Dictionary<leagueUrl, league> data)
        {
            try
            {
                IsBusy = true;

                var total = data.Count;
                int i = 0;
                foreach (var item in data)
                {
                    if (CancelAsync) break;

                    StatusText = "Load matches: " + String.Join("\t",
                                     new string[] { item.Value.name, item.Key.url });
                    await LoadUrlMatches(item.Value, item.Key);

                    i++;
                    ProgressBarValue = 100 * ((double)i / (double)total);
                    await Task.Delay(300);
                }
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }

        //Загружает матчи (.../results) для выбранной ссылки
        //+ добавление в БД
        //+ автосохранение html в .zip файл
        private static async Task LoadUrlMatches(league country, leagueUrl url)
        {
            if (string.IsNullOrEmpty(url.mark))
            {
                MessageBox.Show("Нельзя загружать неотмеченную ссылку");
                return;
            }

            if (LeagueUrlViewModel.GetIsCurrent(url.url))
            {
                MessageBox.Show("Загрузка матчей для текущей лиги реализована в другом месте!");
                return;
            }

            //Проверка на наличие матчей
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<match>();
                if (table.Count(x => x.leagueUrlId == url.id) > 0)
                {
                    Log.Information("Матчи для выбранной ссылки уже существуют {@url}", url);
                    Global.Current.Infos++;
                    return;
                }
            }
            var matches = await BetExplorerParser.GetMatches(country, url);

            //Базовая информативная проверка на некорректные записи, чисто для лога
            var notCorrect = matches.Where(x => !x.IsCorrect).ToList();
            foreach (var beMatch in notCorrect)
            {
                Log.Information("Not correct! {@item}", beMatch);
                Global.Current.Infos++;
            }

            //Добавляем новые значения в соответствующие таблицы
            AddNewTeamnamesToDb(country.id, matches); //dbo.teamNames
            AddNewPossibleResultsToDb(matches); //dbo.possibleResults
            AddNewMatchTagsToDb(matches); //dbo.matchTags

            //Добавляем матчи в базу данных
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var teamsDict = cntx.GetTable<teamName>().
                    Where(x => x.leagueId == country.id).
                    ToDictionary(teamName => teamName.name, teamName => teamName.id);

                var tagsDict = cntx.GetTable<matchTag>()
                    .ToDictionary(matchTag => matchTag.name, matchTag => matchTag.id);

                var matchesTable = cntx.GetTable<match>();
                var possibleResultsDict = cntx.GetTable<possibleResult>().ToDictionary(possibleResult => possibleResult.value,
                    possibleResult => possibleResult.id);

                var toAddMatches = matches.Select(x => new match()
                {
                    leagueId = country.id,
                    leagueUrlId = url.id,
                    date = DateTime.Parse(x.Date),
                    homeTeamId = teamsDict[x.Names[0]],
                    guestTeamId = teamsDict[x.Names[1]],
                    tagId = tagsDict[x.Tag],
                    href = x.Href,
                    matchResultId = possibleResultsDict[x.FinalScore]
                }).ToList();

                matchesTable.InsertAllOnSubmit(toAddMatches);
                cntx.SubmitChanges();
            }
        }
        
        //Вспомогательные методы, используемые при Task LoadMatches
        private static void AddNewMatchTagsToDb(List<BeMatch> matches)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var tagTable = cntx.GetTable<matchTag>();
                var existingTags = tagTable.Select(x => x.name).ToList();

                var newTags = matches.Select(x => x.Tag).Distinct().Where(x => !existingTags.Contains(x)).ToList();

                if (newTags.Any())
                {
                    var toAddRange = newTags.Select(x => new matchTag()
                    {
                        name = x,
                        caption = ""
                    }).ToList();

                    tagTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }

        private static void AddNewPossibleResultsToDb(List<BeMatch> matches)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var resultsTable = cntx.GetTable<possibleResult>();
                var existingResults = resultsTable.Select(x => x.value).ToList();

                var newResults = matches.Select(x => x.FinalScore).Distinct().Where(x => !existingResults.Contains(x)).ToList();

                if (newResults.Any())
                {
                    var toAddRange = new List<possibleResult>();
                    foreach (var newResult in newResults)
                    {
                        int scored;
                        int missed;
                        bool isCorrect;

                        int total = -1;
                        int diff = -1;

                        BetExplorerParser.ParseMatchResult(newResult, out isCorrect, out scored, out missed);
                        if (isCorrect)
                        {
                            total = scored + missed;
                            diff = scored - missed;
                        }

                        var toAdd = new possibleResult()
                        {
                            isCorrect = isCorrect,
                            scored = scored,
                            missed = missed,
                            total = total,
                            diff = diff,
                            value = newResult
                        };
                        toAddRange.Add(toAdd);
                    }

                    resultsTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }

        private static void AddNewPossibleResultsToDb(List<MatchDetail> matchDetails)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var resultsTable = cntx.GetTable<possibleResult>();
                var existingResults = resultsTable.Select(x => x.value).ToList();

                var newResults = new List<possibleResult>();
                foreach (var matchDetail in matchDetails)
                {
                    if (matchDetail == null) continue;

                    if (matchDetail.FirstTimePossibleResult != null && !existingResults.Any(x => x == matchDetail.FirstTimePossibleResult.value))
                    {
                        newResults.Add(matchDetail.FirstTimePossibleResult);
                    }
                    if (matchDetail.SecondTimePossibleResult != null && !existingResults.Any(x => x == matchDetail.SecondTimePossibleResult.value))
                    {
                        newResults.Add(matchDetail.SecondTimePossibleResult);
                    }
                }

                if (newResults.Any())
                {
                    resultsTable.InsertAllOnSubmit(newResults);
                    cntx.SubmitChanges();
                }
            }
        }

        private static void AddNewTeamnamesToDb(int leagueId, List<BeMatch> matches)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var teamNamesTable = cntx.GetTable<teamName>();
                var existingNames = teamNamesTable.Where(x => x.leagueId == leagueId).Select(x => x.name).ToList();

                var newNames = (from item in matches
                                from name in item.Names
                                select name).Distinct().Where(x => !existingNames.Contains(x)).ToList();

                if (newNames.Any())
                {
                    var toAddRange = newNames.Select(x => new teamName()
                    {
                        leagueId = leagueId,
                        name = x
                    })
                        .ToList();

                    teamNamesTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }
        #endregion

        #region Счета таймов
        public IAsyncCommand LoadMatchDetailsCommand { get; private set; }
        public ICommand CoerceResultsCommand { get; private set; }

        /// <summary>
        /// Загружает счета таймов с учетом годовой отсечки (border)
        /// </summary>
        /// <returns></returns>
        private async Task LoadMatchDetails()
        {
            var batches = new List<List<match>>();
            int batchSize = 100;
            ServicePointManager.DefaultConnectionLimit = batchSize;

            var data = GetDataForOperations();

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var allUrls = data.Select(x => x.Key).ToList();

                var borderTable = cntx.GetTable<border>();
                foreach (var brd in borderTable)
                {
                    var except = allUrls.Where(x => x.parentId == brd.leagueId && x.mark == brd.mark)
                        .Where(x => LeagueUrlViewModel.GetPossibleYear(x.year) < brd.resultBorderYear).ToList();

                    allUrls = allUrls.Except(except).ToList();
                }

                var urlsIds = allUrls.Select(x => x.id).ToList();

                var query = (from match in cntx.GetTable<match>()
                             where urlsIds.Contains(match.leagueUrlId)
                             where match.firstHalfResId == null || match.secondHalfResId == null
                             select match).ToList();

                var possibleResDict = cntx.GetTable<possibleResult>().ToDictionary(x => x.id, x => x.isCorrect);
                //Только матчи с корректным итоговым счетом
                query = query.Where(x => possibleResDict[x.matchResultId]).ToList();

                batches = query.Split(batchSize).ToList();
            }

            try
            {
                IsBusy = true;

                string elapsed = "";
                var cnt = batches.Count;
                var totalTime = 0D;
                var sWatch = new Stopwatch();

                for (int i = 0; i < cnt; i++)
                {
                    sWatch.Reset();
                    sWatch.Start();

                    var tasks = batches[i].Select(x => BetExplorerParser.GetHalfsResults(x)).ToList();
                    var matchDetails = await Task.WhenAll(tasks);

                    using (var cntx = new SqlDataContext(Connection.ConnectionString))
                    {
                        //possibleResults
                        AddNewPossibleResultsToDb(matchDetails.ToList());

                        //Updating results
                        var possibleResultDict = cntx.GetTable<possibleResult>().ToDictionary(possibleResult => possibleResult.value,
                            possibleResult => possibleResult.id);

                        var matchesTable = cntx.GetTable<match>();

                        for (int k = 0; k < matchDetails.Length; k++)
                        {
                            if (matchDetails[k] == null)
                            {
                                Log.Warning(batches[i][k].id + " was not parsed");
                                Global.Current.Warnings++;
                                continue;
                            }

                            if (!matchDetails[k].AreResultsCorrect) continue;

                            var match = matchesTable.Single(x => x.id == matchDetails[k].MatchId);
                            match.firstHalfResId = possibleResultDict[matchDetails[k].FirstTimePossibleResult.value];
                            match.secondHalfResId = possibleResultDict[matchDetails[k].SecondTimePossibleResult.value];
                        }
                        cntx.SubmitChanges();
                    }

                    StatusText = "Chunk size: " + batchSize +
                                 ". Number " + i + " from " + cnt + "."
                                 + "Last time: " + elapsed + " s."
                                 + " Finished in: " + (((double)totalTime * ((double)(cnt - i)) / i / 3600D)).ToString("F2") + " h.";

                    ProgressBarValue = 100 * ((double)i / (double)cnt);

                    sWatch.Stop();
                    elapsed = sWatch.Elapsed.Seconds.ToString();
                    totalTime += sWatch.Elapsed.Seconds;

                    if (CancelAsync) break;
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, "LoadMatchDetails");
                Global.Current.Errors++;
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }


        /// <summary>
        /// Корректирует счета таймов (см. описание внутри)
        /// </summary>
        /// <param name="obj"></param>
        private void CoerceResults(object obj)
        {
            //Correct ET + PEN
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var possibleResults = cntx.GetTable<possibleResult>();
                var penOrEtIds = possibleResults.Where(x => x.value.Contains("PEN") || x.value.Contains("ET")).
                    Select(x => x.id).ToList();

                //НеNULLевые счета таймов, но при этом notCorrect итоговый счет
                var listToCoerce = (from match in cntx.GetTable<match>()
                                    where match.firstHalfResId != null && match.secondHalfResId != null
                                    where penOrEtIds.Contains(match.matchResultId)
                                    select new
                                    {
                                        MatchId = match.id,
                                        FullTimeRes = match.matchResultId,
                                        FirstHalfRes = match.firstHalfResId.Value,
                                        SecondHalfRes = match.secondHalfResId.Value
                                    }).ToList();

                foreach (var match in listToCoerce)
                {
                    var res = possibleResults.Single(x => x.id == match.FullTimeRes);

                    var psbl1 = possibleResults.Single(x => x.id == match.FirstHalfRes);
                    var psbl2 = possibleResults.Single(x => x.id == match.SecondHalfRes);

                    var psbl = possibleResults.Where(x => x.scored == (psbl1.scored + psbl2.scored) &&
                                                        x.missed == (psbl1.missed + psbl2.missed)
                                                        );
                    if (psbl.Count() > 1) throw new Exception();
                    if (psbl.Count() == 0)
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

                    var toUpdate = cntx.GetTable<match>().Single(x => x.id == match.MatchId);
                    toUpdate.beforeCoercedId = toUpdate.matchResultId;
                    cntx.SubmitChanges();
                    toUpdate.matchResultId = newPsbl.id;
                    cntx.SubmitChanges();
                }
            }

            //CORRECT AWA, CAN.
            //(Когда счета таймов есть, а итоговый счет тю-тю) Нужно обнулить счета таймов
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var possibleResults = cntx.GetTable<possibleResult>();
                var awaIds = possibleResults.Where(x => x.value.Contains("AWA") || x.value.Contains("CAN")).
                    Select(x => x.id).ToList();

                var matches = (from match in cntx.GetTable<match>()
                               where match.firstHalfResId != null && match.secondHalfResId != null
                               where awaIds.Contains(match.matchResultId)
                               select match).ToList();

                int i = 0;
                matches.ForEach(x =>
                {
                    x.firstHalfResId = null;
                    x.secondHalfResId = null;
                    i++;
                });
                cntx.SubmitChanges();
                MessageBox.Show("Исправлено штук, " + i.ToString());
            }
        }
        #endregion

        #region Отметки
        public ICommand MarkSelectedUrlsCommand { get; private set; }
        public ICommand UnmarkSelectedUrlsCommand { get; private set; }
        public ICommand MarkAutoCommand { get; private set; }
        public ICommand CheckMarksCommand { get; private set; }

        private void MarkSelectedUrls(object a, string mark)
        {
            IList items = (IList)a;
            var leagueUrls = items.Cast<LeagueUrlViewModel>();
            var ids = leagueUrls.Select(x => x.Source.id).ToList();

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();

                foreach (var id in ids)
                {
                    var single = table.Single(x => x.id == id);
                    single.mark = mark;

                    var cur = LeagueUrls.Items.Single(x => x.Source.id == id);
                    cur.Source.mark = mark;

                }
                cntx.SubmitChanges();
            }
        }
        private void MarkAuto(object a)
        {
            var name = LeagueUrls.Selected.Source.name;
            var mark = SelectedLeagueMark.name;

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();

                foreach (var leagueUrlViewModel in LeagueUrls.Items)
                {
                    if (leagueUrlViewModel.Source.name != name) continue;

                    var single = table.Single(x => x.id == leagueUrlViewModel.Source.id);
                    single.mark = mark;

                    leagueUrlViewModel.Source.mark = mark;
                }

                cntx.SubmitChanges();
            }
        }
        private void CheckMarks(object a)
        {
            var uniqueMarks = LeagueUrls.Items.Select(x => x.Source.mark).Distinct().Where(x => x.Length > 0).ToList();
            if (!uniqueMarks.Any()) return;

            bool hasErrors = false;
            foreach (var uniqueMark in uniqueMarks)
            {
                var items = LeagueUrls.Items.
                    Where(x => x.Source.mark == uniqueMark).
                    OrderBy(x => x.PossibleYear)
                    .ToList();

                for (int i = 1; i < items.Count; i++)
                {
                    if ((items[i].PossibleYear - items[i - 1].PossibleYear) != 1)
                    {
                        var item = items[i];
                        Log.Warning("Problem with {@item}", item);
                        Global.Current.Warnings++;
                        hasErrors = true;
                    }
                }
            }

            StatusText = Selected.name + (hasErrors ? ": Были обнаружены ошибки, смотри лог" : ": Ошибок нет");
        }

        #endregion

        #region Other
        //В зависимости от выбранного режима загрузки (и турнира, при необходимости) выбирает коллекцию [leagueUrl, league] для загрузки
        private Dictionary<leagueUrl, league> GetDataForOperations()
        {
            var result = new Dictionary<leagueUrl, league>();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var countries = cntx.GetTable<league>();
                var urlTable = cntx.GetTable<leagueUrl>();
                var mark = SelectedLeagueMark.name;

                //Это перечень всех всех всех
                var tpl =
                    (from league in countries
                     join leagueUrl in urlTable on league.id equals leagueUrl.parentId
                     where league.isFavorite &&
                           leagueUrl.mark.Length > 1
                     let isCur = LeagueUrlViewModel.GetIsCurrent(leagueUrl.url)
                     select new Tuple<league, leagueUrl, bool>(league, leagueUrl, isCur)).ToList();

                tpl = tpl.Where(x => !x.Item3).ToList();

                //Выбранная ссылка
                if (SelectedMatchLoadMode == Global.MatchLoadModes[0])
                {
                    if (LeagueUrls.Selected.Source.mark == mark)
                    {
                        var toAdd = tpl.SingleOrDefault(x => x.Item2.id == LeagueUrls.Selected.Source.id);
                        if (toAdd != null) result.Add(toAdd.Item2, toAdd.Item1);
                    }
                }
                //Турнир для страны
                if (SelectedMatchLoadMode == Global.MatchLoadModes[1])
                {
                    var toAdd = tpl.Where(x => x.Item1.id == Selected.id && x.Item2.mark == mark).ToList();
                    foreach (var tuple in toAdd)
                    {
                        result.Add(tuple.Item2, tuple.Item1);
                    }
                }
                //Турнир для всех стран
                if (SelectedMatchLoadMode == Global.MatchLoadModes[2])
                {
                    var toAdd = tpl.Where(x => x.Item2.mark == mark).ToList();
                    foreach (var tuple in toAdd)
                    {
                        result.Add(tuple.Item2, tuple.Item1);
                    }
                }
                //Все турниры везде-везде
                if (SelectedMatchLoadMode == Global.MatchLoadModes[3])
                {
                    foreach (var tuple in tpl)
                    {
                        result.Add(tuple.Item2, tuple.Item1);
                    }
                }
            }
            return result;
        }


        public IAsyncCommand SetUrlsRepoCommand { get; private set; }
        //Загружает список ссылок для выбранной страны
        private async Task SetUrlsRepo()
        {
            await Task.Run(() =>
            {
                LeagueUrls.Update(Selected);

                Application.Current.Dispatcher.Invoke(delegate
                {
                    FlagPath = "/Images/Flags/" + _selected.svgName;
                    IsFavorite = _selected.isFavorite;
                    OnPropertyChanged("LeagueUrls");
                    OnPropertyChanged("IsFavorite");
                });

            });
        }

        #endregion

        #region DangerZone
        public ICommand RemoveLeagueDataCommand { get; }

        private void RemoveLeagueData(object obj)
        {
            var leagueId = Selected.id;
            var mark = SelectedLeagueMark.name;

            var dlg = MessageBox.Show("Очистить данные для " + Selected.name + " - " + mark + "?", "",
                MessageBoxButton.YesNo);
            if (dlg == MessageBoxResult.Yes)
            {
                var output = "";
                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var leagueUrlTable = cntx.GetTable<leagueUrl>();
                    //Демаркируем leagueUrl
                    var itemsToDemark = leagueUrlTable.Where(x => x.parentId == leagueId && x.mark == mark);
                    foreach (var leagueUrl in itemsToDemark)
                    {
                        leagueUrl.mark = "";
                    }

                    output = "Сняты отметки с " + itemsToDemark.Count() + " шт." + "\n";

                    //Удаляем odds
                    var urlIds = itemsToDemark.ToList().Select(x => x.id).ToList();

                    var oddsTable = cntx.GetTable<odd>();

                    var oddsToDelete = from o in oddsTable
                                       where
                            (from m in cntx.GetTable<match>()
                                where urlIds.Contains(m.leagueUrlId)
                                select m.id
                            ).Contains(o.parentId)
                        select o;

                    output += "Удалено кэфов " + oddsToDelete.Count() + " шт." + "\n";

                    oddsTable.DeleteAllOnSubmit(oddsToDelete);
                    
                    //Удаляем teamNames, причем только те, которые используются исключительно для выбранной mark
                    var teamNamesTable = cntx.GetTable<teamName>();

                    var markIds = ((from m in cntx.GetTable<match>()
                            where urlIds.Contains(m.leagueUrlId)
                            select m.homeTeamId
                        ).Union(
                            from m in cntx.GetTable<match>()
                            where urlIds.Contains(m.leagueUrlId)
                            select m.guestTeamId
                        )).Distinct().ToList();

                    var restIds = ((from m in cntx.GetTable<match>()
                            where !urlIds.Contains(m.leagueUrlId)
                            select m.homeTeamId
                        ).Union(
                            from m in cntx.GetTable<match>()
                            where !urlIds.Contains(m.leagueUrlId)
                            select m.guestTeamId
                        )).Distinct().ToList();

                    var toDeleteIds = markIds.Except(restIds).ToList();

                    var namesToDelete = from n in teamNamesTable
                        where toDeleteIds.Contains(n.id)
                        select n;

                    output += "Удалено имен команд " + namesToDelete.Count() + " шт." + "\n";

                    teamNamesTable.DeleteAllOnSubmit(namesToDelete);
                    
                    //Удаляем матчи
                    var matchesTable = cntx.GetTable<match>();
                    var matchesToDelete = from m in matchesTable
                        where urlIds.Contains(m.leagueUrlId)
                        select m;

                    output += "Удалено матчей " + matchesToDelete.Count() + " шт." + "\n";
                    matchesTable.DeleteAllOnSubmit(matchesToDelete);

                    cntx.SubmitChanges();

                    MessageBox.Show(output);
                }
            }
        }


        #endregion

        #region Кэфы
        public IAsyncCommand Load1x2CoefsCommand { get; private set; }
        public IAsyncCommand LoadOuCoefsCommand { get; private set; }
        public IAsyncCommand LoadForaCoefsCommand { get; private set; }
        public IAsyncCommand LoadBtsCoefsCommand { get; private set; }

        private async Task Load1x2Coefs()
        {
            //Загружаем только те URL, Для которых mark != ""
            try
            {
                IsBusy = true;

                List<Tuple<league, leagueUrl, bool>> tpl;

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var countries = cntx.GetTable<league>();
                    var urlTable = cntx.GetTable<leagueUrl>();

                    tpl =
                        (from league in countries
                            join leagueUrl in urlTable on league.id equals leagueUrl.parentId
                            where league.isFavorite && leagueUrl.mark.Length > 1
                            let isCur = LeagueUrlViewModel.GetIsCurrent(leagueUrl.url)
                            select new Tuple<league, leagueUrl, bool>(league, leagueUrl, isCur)).ToList();

                    tpl = tpl.Where(x => !x.Item3).ToList();
                    tpl = tpl.Where(x => LeagueUrlViewModel.GetPossibleYear(x.Item2.year) >= Settings.Default.oddLoadYear).ToList();
                }

                var total = tpl.Count;
                int i = 0;

                foreach (var item in tpl)
                {
                    if (CancelAsync) break;

                    StatusText = "Загружаем 1х2 кэфы из файла для ..." + String.Join("\t",
                        new string[] { item.Item1.name, item.Item2.url, item.Item2.year });

                    var matches = await BetExplorerParser.GetMatches(item.Item1, item.Item2);

                    var hrefs = matches.Where(x => x.Odds.Count == 3).Select(x => x.Href).ToList();
                    if (matches.Count != hrefs.Count)
                    {
                        var dif = matches.Count - hrefs.Count;
                        Log.Information(item.Item1.name + "\t" + item.Item2.name + "\t" + dif.ToString());
                        Global.Current.Infos++;
                    }
                    
                    using (var cntx = new SqlDataContext(Connection.ConnectionString))
                    {
                        var matchesTable = cntx.GetTable<match>();
                        
                        var matchesToAddOdds = matchesTable.
                            Where(x => hrefs.Contains(x.href)).
                            ToList();

                        var oddsTable = cntx.GetTable<odd>();

                        var oddTypeList = new List<string>() {OddType._1, OddType.X, OddType._2};

                        for (int j = 0; j < oddTypeList.Count; j++)
                        {
                            var toAddOdds = (from m in matchesToAddOdds
                                let ma = matches.Single(x => x.Href == m.href)
                                select new odd()
                                {
                                    oddType = oddTypeList[j],
                                    parentId = m.id,
                                    value = ma.Odds[j]
                                }).ToList();

                            oddsTable.InsertAllOnSubmit(toAddOdds);
                            cntx.SubmitChanges();
                        }
                    }

                    i++;
                    ProgressBarValue = 100 * ((double)i / (double)total);
                    await Task.Delay(100);
                }
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }
        
        private async Task LoadCoefs(BeOddLoadMode mode)
        {
            try
            {
                IsBusy = true;
                string oddTypeKeyword = OddType.GetOddTypeKeyword(mode);

                var data = GetDataForOperations().Where(x =>
                    LeagueUrlViewModel.GetPossibleYear(x.Key.year) >= Settings.Default.oddLoadYear).ToList();
               
                int batchSize = 100;
                ServicePointManager.DefaultConnectionLimit = batchSize;

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var leagueUrlsIds = data.Select(x => x.Key.id).ToList();

                    var notCorrectResIds = cntx.GetTable<possibleResult>().
                        Where(x => !x.isCorrect).Select(x => x.id)
                        .ToList();

                    var notLoadedIds = ((from m in cntx.GetTable<match>()
                        where leagueUrlsIds.Contains(m.leagueUrlId) && !notCorrectResIds.Contains(m.matchResultId)
                        select m.id).Except
                    (
                        (from o in cntx.GetTable<odd>()
                            where o.oddType.Contains(oddTypeKeyword)
                            select o.parentId).Distinct()
                    )).ToList();
                    
                    var idBatches = notLoadedIds.Split(batchSize).ToList();
                    
                    string elapsed = "";
                    var cnt = idBatches.Count;
                    var totalTime = 0D;
                    var sWatch = new Stopwatch();
                    
                    for (int j = 0; j < cnt; j++)
                    {
                        sWatch.Reset();
                        sWatch.Start();

                        var matchesToParse = (from m in cntx.GetTable<match>()
                            where idBatches[j].Contains(m.id)
                            select m).ToList();

                        var tasks = matchesToParse.Select(x => BetExplorerParser.GetMatchOdds(x, mode)).ToList();
                        var oddsToAdd = await Task.WhenAll(tasks);
                        
                        var oddsTable = cntx.GetTable<odd>();
                        for (int k = 0; k < oddsToAdd.Length; k++)
                        {
                            if (oddsToAdd[k].Any() && oddsToAdd[k].All(z => z != null))
                            {
                                oddsTable.InsertAllOnSubmit(oddsToAdd[k]);
                            }
                        }

                        cntx.SubmitChanges();
                        
                        sWatch.Stop();
                        StatusText = "Mode: " + mode.ToString() + "\r\n" +
                                     "Chunk size: " + batchSize +
                                     ". Number " + j + " from " + cnt + "."
                                     + "Last time: " + elapsed + " s."
                                     + " Finished in: " +
                                     (((double) totalTime * ((double) (cnt - j)) / j / 3600D)).ToString("F2") + " h.";

                        ProgressBarValue = 100 * ((double) j / (double) cnt);
                        sWatch.Stop();
                        elapsed = sWatch.Elapsed.Seconds.ToString();
                        totalTime += sWatch.Elapsed.Seconds;
                    }

                }
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }
        #endregion

        #region ToDelete
        public IAsyncCommand TestAsyncCommand { get; private set; }


        private async Task TestAsyncTask()
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var ids = cntx.GetTable<odd>().Where(x => x.oddType == "Over 2.5").
                    Take(100).Select(x => x.parentId).Distinct().ToList();

                var matches = cntx.GetTable<match>().Where(x => ids.Contains(x.id)).ToList();

                //begin to experiment
                ServicePointManager.DefaultConnectionLimit = 20;

                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                };


                var client = new HttpClient(handler);

                var href = matches[0].href;
                var oddLoadMode = BeOddLoadMode.OU;

                var matchId = href.Split('/').Last(x => !String.IsNullOrEmpty(x));
                string url = "https://www.betexplorer.com/match-odds/" + matchId + "/1/" + oddLoadMode + "/";

               // var tst = await BetExplorerParser.GetMatchOdds(matches[0], BeOddLoadMode.OU);


                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36");
                client.DefaultRequestHeaders.Add("Referer", Settings.Default.beUrl + href.Substring(1, href.Length - 1));
                client.DefaultRequestHeaders.Add("method", "GET");
                client.DefaultRequestHeaders.Add("authority", "www.betexplorer.com");
                client.DefaultRequestHeaders.Add("scheme", "https");
                client.DefaultRequestHeaders.Add("path", "/match-odds/" + matchId + "/1/" + oddLoadMode);
                client.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");
                client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                client.DefaultRequestHeaders.Add("accept-language", "en-US;q=0.8,ru-RU,ru;q=0.9,en;q=0.7");

                var result = await client.GetAsync(Settings.Default.beUrl + href.Substring(1, href.Length - 1));
                int z = 3;

                /*
                var request = (HttpWebRequest)WebRequest.Create(url);
                //request.Accept = "application/json, text/javascript, #2#*; q=0.01";
                //request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36";
                //request.Referer = Settings.Default.beUrl + href.Substring(1, href.Length - 1);
               // request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                request.Headers.Add("method", "GET");
                request.Headers.Add("authority", "www.betexplorer.com");
                request.Headers.Add("scheme", "https");
                request.Headers.Add("path", "/match-odds/" + matchId + "/1/" + oddLoadMode);
                request.Headers.Add("x-requested-with", "XMLHttpRequest");
                request.Headers.Add("sec-fetch-site", "same-origin");
                request.Headers.Add("sec-fetch-mode", "cors");
                request.Headers.Add("sec-fetch-dest", "empty");
                request.Headers.Add("accept-language", "en-US;q=0.8,ru-RU,ru;q=0.9,en;q=0.7");*/





            }



            /*var t = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                t.Add(SendRequest(client, "http://slowwly.robertomurray.co.uk/delay/5000/url/https://habr.com"));
            }

            Task.WaitAll(t.ToArray());*/
            /*var ids = File.ReadAllLines("input.txt").Select(x => int.Parse(x)).ToList();
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var matches = cntx.GetTable<match>().Where(x => ids.Contains(x.id)).ToList();

                foreach (var match in matches)
                {
                    int z = 3;
                    var odds = await BetExplorerParser.GetMatchOdds(match, BeOddLoadMode.OU);
                }
            }*/
        }

        public ICommand TestCommand { get; set; }

        private void Test(object a)
        {



            /*var country = Selected;
            var url = LeagueUrls.Selected;

            //Проверка на наличие матчей
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<match>();
                if (table.Count(x => x.parentId == url.Source.id) > 0)
                {
                    Log.Information("Матчи для выбранной ссылки уже существуют {@url}", url);
                    Global.Current.Infos++;
                    return;
                }
            }

            var matches = BetExplorerParser.GetMatches(country, url.Source).Result;

            //Базовая информативная проверка на некорректные записи, чисто дяя лога
            var notCorrect = matches.Where(x => !x.IsCorrect).ToList();
            foreach (var beMatch in notCorrect)
            {
                Log.Information("Not correct! {@item}", beMatch);
                Global.Current.Infos++;
            }

            //Добавляем новые значения в соответствующие таблицы
            AddNewTeamnamesToDb(matches); //dbo.teamNames
            AddNewPossibleResultsToDb(matches); //dbo.possibleResults
            AddNewMatchTagsToDb(matches); //dbo.matchTags

            var parentId = url.Source.id;

            //Добавляем матчи в базу данных
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var teamsDict = cntx.GetTable<teamName>().
                    Where(x => x.leagueId == country.id).
                    ToDictionary(teamName => teamName.name, teamName => teamName.id);

                var tagsDict = cntx.GetTable<matchTag>()
                    .ToDictionary(matchTag => matchTag.name, matchTag => matchTag.id);


                var matchesTable = cntx.GetTable<match>();

                var toAddMatches = matches.Select(x => new match()
                {
                    parentId = parentId,
                    date = DateTime.Parse(x.Date),
                    homeTeamId = teamsDict[x.Names[0]],
                    guestTeamId = teamsDict[x.Names[1]],
                    tagId = tagsDict[x.Tag],
                    href = x.Href
                }).ToList();

                matchesTable.InsertAllOnSubmit(toAddMatches);
                cntx.SubmitChanges();

                //Adding results
                var resultsTable = cntx.GetTable<result>();
                var resultsDict = cntx.GetTable<possibleResult>().ToDictionary(possibleResult => possibleResult.value,
                    possibleResult => possibleResult.id);

                var toAddResults = toAddMatches.
                    Select((t, i) => new result()
                    {
                        parentId = t.id,
                        matchPeriod = 0,
                        resultId = resultsDict[matches[i].FinalScore]
                    }).ToList();

                resultsTable.InsertAllOnSubmit(toAddResults);
                cntx.SubmitChanges();
            }*/
        }


        #endregion

    }
}

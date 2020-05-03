﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;
using FinalBet.Model;
using FinalBet.Properties;
using Serilog;

namespace FinalBet.ViewModel
{

    public class DatabaseViewModel: ViewModelBase
    {
        #region Variables
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


        private league _selected;
        public league Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged("Selected");
                //LeagueUrls.Update(Selected);
            }
        }

        private leagueMark _selectedLeagueMark;
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
            var ctr = (league) a;
            return ctr.isFavorite;

        }

        private bool _isFavorite;
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

        #endregion

        #region AsyncCommands
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

        private double _pBarValue;
        public double ProgressBarValue
        {
            get => _pBarValue;
            set
            {
                if (Math.Abs(_pBarValue - value) < 0.0001) return;
                _pBarValue = value;
                OnPropertyChanged("ProgressBarValue");
            }
        }

        public ICommand BreakCommand
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
                    a=> IsBusy); }
        }
        public IAsyncCommand LoadUrlsCommand { get; private set; }
        public IAsyncCommand LoadAllUrlsCommand { get; private set; }
        public IAsyncCommand LoadMatchesCommand { get; private set; }
        public IAsyncCommand LoadMarkedMatchesCommand { get; private set; }
        public IAsyncCommand LoadUrlsRepoCommand { get; private set; }

        public async Task LoadUrlsRepo()
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

        public async Task LoadUrls(league ctr)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                bool anyItemExists = table.Any(x => x.parentId == ctr.id);

                var country = ctr.name;
                if (anyItemExists)
                {
                    Log.Warning("LoadUrls, table not empty! {@country}", country);
                    Global.Current.Warnings++;
                }
                else
                {
                    try
                    {
                        IsBusy = true;
                        StatusText = "Начинаю загрузку ссылок для " + ctr.name;

                        var html = await BetExplorerParser.GetLeagueUrlsHtml(ctr);

                        var urls = BetExplorerParser.GetLeagueUrls(html, ctr.id);
                        table.InsertAllOnSubmit(urls);
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
        }
        public async Task LoadAllUrls()
        {
            try
            {
                IsBusy = true;
                var total = Items.Count;
                int i = 0;
                foreach (var country in Items)
                {
                    if (CancelAsync) break;
                    
                    await LoadUrls(country);

                    i++;
                    ProgressBarValue = 100*((double) i / (double) total);
                }
            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }

        public async Task LoadMatches(league country, leagueUrl url)
        {
            //Проверка на наличие матчей
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<match>();
                if (table.Count(x => x.parentId == url.id) > 0)
                {
                    Log.Information("Матчи для выбранной ссылки уже существуют {@url}", url);
                    Global.Current.Infos++;
                    return;
                }
            }

            var matches = await BetExplorerParser.GetMatches(country, url);

            int z = 3;
            return;
            
            //Базовая информативная проверка на некорректные записи
            var notCorrect = matches.Where(x => !x.IsCorrect).ToList();
            foreach (var beMatch in notCorrect)
            {
                Log.Information("Not correct! {@item}", beMatch);
                Global.Current.Infos++;
            }

            //Добавляем новые значения в соответствующие таблицы
            AddNewTeamnamesToDb(matches); //dbo.teamNames
            AddNewResultsToDb(matches); //dbo.possibleResults
            AddNewMatchTagsToDb(matches); //dbo.matchTags

            var parentId = url.id;


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
                    href = x.Href,
                    other = ""
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
                        resultId = resultsDict[matches[i].FinalScore],
                        other = ""
                    }).ToList();

                resultsTable.InsertAllOnSubmit(toAddResults);
                cntx.SubmitChanges();
            }
        }

        public async Task LoadMarkedMatches()
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
                            select new Tuple<league,leagueUrl,bool>(league,leagueUrl,isCur)).ToList();

                    tpl = tpl.Where(x => !x.Item3).ToList();
                }

                /*File.WriteAllLines("tst.txt", tpl.Select(x=>string.Join("\t", new string[]{x.Item1.name, x.Item2.url, x.Item2.year})));
                return;*/

                var total = tpl.Count;
                int i = 0;
                foreach (var item in tpl)
                {
                    if (CancelAsync) break;

                    StatusText = "Парсим..." + string.Join("\t",
                        new string[] {item.Item1.name, item.Item2.url, item.Item2.year});
                    await LoadMatches(item.Item1, item.Item2);

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

        private void AddNewMatchTagsToDb(List<BeMatch> matches)
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
                        caption = "",
                        other = ""
                    }).ToList();

                    tagTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }

        private void AddNewResultsToDb(List<BeMatch> matches)
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
                            value = newResult,
                            other = ""
                        };
                        toAddRange.Add(toAdd);
                    }

                    resultsTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }

        private void AddNewTeamnamesToDb(List<BeMatch> matches)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var teamNamesTable = cntx.GetTable<teamName>();
                var existingNames = teamNamesTable.Where(x => x.leagueId == Selected.id).Select(x => x.name).ToList();

                var newNames = (from item in matches
                                from name in item.Names
                                select name).Distinct().Where(x => !existingNames.Contains(x)).ToList();

                if (newNames.Any())
                {
                    var toAddRange = newNames.Select(x => new teamName()
                    {
                        leagueId = Selected.id,
                        name = x,
                        other = ""
                    })
                        .ToList();

                    teamNamesTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }
        }
        #endregion

        #region Commands

        public ICommand TestCommand { get; private set; }

        public ICommand OpenArchiveFolderCommand
        {
            get
            {
                return new RelayCommand((o => Process.Start(Settings.Default.zipFolder) ), x=> Directory.Exists(Settings.Default.zipFolder) );
            }
        }
        public ICommand ShowFileDetailsCommand { get; private set; }
        
        public ICommand MarkSelectedUrlsCommand { get; private set; }
        public ICommand UnmarkSelectedUrlsCommand { get; private set; }
        public ICommand MarkAutoCommand { get; private set; }
        public ICommand CheckMarksCommand { get; private set; }

        public IAsyncCommand StressTestCommand { get; private set; }

        private async Task StressTest()
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var a = 3000;
                var b = 3000;
                var table = cntx.GetTable<odd>();

                var rand = new Random();
                var addRange = new List<odd>();

                await Task.Run((() =>
                {
                    for (int i = 0; i < a; i++)
                    {
                        addRange.Clear();
                        for (int j = 0; j < b; j++)
                        {
                            var rnd = rand.NextDouble();
                            var toAdd = new odd
                            {
                                parentId = i,
                                value = rnd + 2,
                                oddType = rnd < 0.3 ? "1" : (rnd > 0.7 ? "2" : "X"),
                                other = ""
                            };
                            addRange.Add(toAdd);
                        }
                        Application.Current.Dispatcher.Invoke(delegate { StatusText = i*1000 + " из " + a * b; });
                        table.InsertAllOnSubmit(addRange);
                        cntx.SubmitChanges();
                    }
                }));
            }
        }



        public void Test(object a)
        {
            MessageBox.Show("!");
        }
        
        private void MarkSelectedUrls(object a, string mark)
        {
            System.Collections.IList items = (System.Collections.IList)a;
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
                    if(leagueUrlViewModel.Source.name != name) continue;
                    
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

            StatusText = Selected.name + (hasErrors ?  ": Были обнаружены ошибки, смотри лог" : ": Ошибок нет");
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
                    item.MatchesCount = matches.Count(x => x.parentId == item.Source.id);
                }
            }
        }

        #endregion

        public DatabaseViewModel()
        {
            // TODO: leagueUrl: ShowMatches 
            // TODO: loadMatches for allLeagues (marked or separate list)
            // TODO: удалить other где точно не нужно

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

            TestCommand = new RelayCommand(Test);
            //AsyncCommands
            LoadUrlsCommand = new AsyncCommand(()=> LoadUrls(Selected), () => Selected != null && !IsBusy);
            LoadAllUrlsCommand = new AsyncCommand(LoadAllUrls, () => Items.Any() && !IsBusy);
            LoadMatchesCommand = new AsyncCommand(()=>LoadMatches(Selected, LeagueUrls.Selected.Source),
                () => Selected != null && LeagueUrls.Items.Any() && LeagueUrls.Selected != null && !IsBusy);
            LoadMarkedMatchesCommand = new AsyncCommand(LoadMarkedMatches);

            LoadUrlsRepoCommand = new AsyncCommand(LoadUrlsRepo);

            //Commands
            MarkSelectedUrlsCommand = new RelayCommand(x=> MarkSelectedUrls(x, SelectedLeagueMark.name), 
                                    a => LeagueUrls.Selected != null && SelectedLeagueMark != null);
            UnmarkSelectedUrlsCommand = new RelayCommand(x=> MarkSelectedUrls(x, ""), a=>LeagueUrls.Selected != null);
            MarkAutoCommand = new RelayCommand(MarkAuto, a=> LeagueUrls.Items.Any() && SelectedLeagueMark != null);
            CheckMarksCommand = new RelayCommand(CheckMarks);

            ShowFileDetailsCommand = new RelayCommand(ShowFileDetails, a=> LeagueUrls.Items.Any());

            StressTestCommand = new AsyncCommand(StressTest);
        }


    }
}

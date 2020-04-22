using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;
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


        private league _selected = null;
        public league Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                FlagPath = "/Images/Flags/" + _selected.svgName;
                LeagueUrls.Update(value);
                OnPropertyChanged("Selected");
                OnPropertyChanged("LeagueUrls");
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

        #endregion


        #region AsyncCommands
        private bool _isBusy = false;
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

        private double _pBarValue = 0;
        public double ProgressBarValue
        {
            get => _pBarValue;
            set
            {
                if (_pBarValue == value) return;
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
                        IsBusy = false;
                    },
                    a=> IsBusy); }
        }

        public IAsyncCommand LoadUrlsCommand { get; private set; }
        public IAsyncCommand TestAsyncCommand { get; private set; }
        public IAsyncCommand LoadAllUrlsCommand { get; private set; }

        public async Task LoadUrls(league cntr)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                bool anyItemExists = table.Any(x => x.parentId == cntr.id);

                var country = cntr.name;
                if (anyItemExists)
                {
                    Log.Warning("LoadUrls, table not empty! {@country}", country);
                }
                else
                {
                    try
                    {
                        IsBusy = true;
                        StatusText = "Начинаю загрузку ссылок для " + cntr.name;

                        var html = await BetExplorerParser.GetLeagueUrlsHtml(cntr);

                        var urls = BetExplorerParser.GetLeagueUrls(html, cntr.id);
                        table.InsertAllOnSubmit(urls);
                        cntx.SubmitChanges();

                        StatusText = "Успешно!";
                        Log.Information("LeagueUrls загружены для страны {@country}", country);
                    }
                    catch (Exception ex)
                    {
                        StatusText = "Возникло исключение, смотри логи!";
                        Log.Warning(ex, "Task LoadUrls()");
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
            IsBusy = true;
            try
            {
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


        private async Task ExecuteSubmitAsync()
        {
            try
            {
                IsBusy = true;
                StatusText = "Начинаю загрузочку";

                for (int i = 0; i < 15; i++)
                {
                    if (CancelAsync) break;
                    await Task.Delay(1000);
                    StatusText = i + " ...";
                }

                StatusText = CancelAsync ? "Загрузочка прервана" : "Загрузочка завершена";

            }
            finally
            {
                IsBusy = false;
                CancelAsync = false;
            }
        }
        #endregion

        #region Commands
        
        public ICommand TestCommand { get; private set; }
        public ICommand LoadMatchesCommand { get; private set; }
        public ICommand MarkSelectedUrlsCommand { get; private set; }


        public void Test(object a)
        {
            CancelAsync = true;
        }

        
        public void LoadMatches(object a)
        {
           var matches = BetExplorerParser.GetMatches(Selected, LeagueUrls.Selected.Source);

           //Базовая проверка на некорректные записи
           var notCorrect = matches.Where(x => !x.IsCorrect).ToList();
           foreach (var beMatch in notCorrect)
           {
               Log.Information("Not correct! {@item}", beMatch);
           }

           //Добавляем новые значения в соответствующие таблицы
           AddNewTeamnamesToDb(matches); //dbo.teamNames
           AddNewResultsToDb(matches); //dbo.possibleResults
           AddNewMatchTagsToDb(matches); //dbo.matchTags

           var parentId = LeagueUrls.Selected.Source.id;

           
           //Добавляем матчи в базу данных
           using (var cntx = new SqlDataContext(Connection.ConnectionString))
           {
               var teamsDict = cntx.GetTable<teamName>().
                   Where(x => x.leagueId == Selected.id).
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

               //Adding odds
               var oddsTable = cntx.GetTable<odd>();
               var oddTypes = new Dictionary<int, string> {{0, "1"}, {1, "X"}, {2, "2"}};
               for (int k = 0; k < 3; k++)
               {
                   var toAddOdds = toAddMatches.
                       Select((t, i) => new odd()
                       {
                           parentId = t.id,
                           oddType = oddTypes[k],
                           value = double.Parse(matches[i].Odds[k]),
                           other = ""
                       }).ToList();
                   oddsTable.InsertAllOnSubmit(toAddOdds);
                   cntx.SubmitChanges();
               }
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

        private void MarkSelectedUrls(object a)
        {
            System.Collections.IList items = (System.Collections.IList)a;
            var leagueUrls = items.Cast<LeagueUrlViewModel>();
            var ids = leagueUrls.Select(x => x.Source.id).ToList();

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();

                var mark = SelectedLeagueMark.name;
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

        #endregion

        public DatabaseViewModel()
        {
            // TODO: leagueUrl: ShowMatches 
            // TODO: Automark leagueUrl
            // TODO: possible year
            // TODO: loadMatches async
            // TODO: loadMatches for allLeagues (marked or separate list)
            // TODO: IsBusy чтобы были недоступны другие команды, когда идет async задача
            // TODO: удалить other где точно не нужно
            // TODO: iMatch??? id, score, losted
            // TODO: GetOuput(iMatch, code)
            // TODO: Code => UserControl + Class


            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<league>().ToList();
                foreach (var league in table)
                {
                    Items.Add(league);
                }
            }

            if (Items.Any()) Selected = Items[0];

            //Commands
            LoadUrlsCommand = new AsyncCommand(()=> LoadUrls(Selected), () => Selected != null && !IsBusy);
            LoadAllUrlsCommand = new AsyncCommand(LoadAllUrls, () => Items.Any() && !IsBusy);
            TestAsyncCommand = new AsyncCommand(ExecuteSubmitAsync, () => !IsBusy && !IsBusy);

            
            TestCommand = new RelayCommand(Test, a=> Selected!=null);
            LoadMatchesCommand = new RelayCommand(LoadMatches,
                a => Selected != null && LeagueUrls.Items.Any() && LeagueUrls.Selected != null);
            MarkSelectedUrlsCommand = new RelayCommand(MarkSelectedUrls, a => LeagueUrls.Selected != null);

            
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            get
            {
                return _flagPath;
            }
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
            get
            {
                return _items;
            }
            set
            {
                if (_items == value) return;
                _items = value;
                OnPropertyChanged("Items");
            }
        }

        private LeagueUrlRepoViewModel _leagueUrls = new LeagueUrlRepoViewModel(null);
        public LeagueUrlRepoViewModel LeagueUrls
        {
            get
            {
                return _leagueUrls;
            }
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
            get
            {
                return _selected;
            }
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




        #endregion

        #region Commands
        public ICommand TestCommand { get; private set; }
        public ICommand LoadUrlsCommand { get; private set; }
        public ICommand LoadMatchesCommand { get; private set; }


        public void Test(object a)
        {
            
        }

        public void LoadUrls(object a)
        {
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                bool anyItemExists = table.Any(x => x.parentId == Selected.id);

                var country = Selected.name;
                if (anyItemExists)
                {
                    Log.Warning("LoadUrls, table not empty! {@country}", country);
                    return;
                }
                else
                {
                    var urls = BetExplorerParser.GetLeagueUrls(Selected);
                    table.InsertAllOnSubmit(urls);
                    cntx.SubmitChanges();
                    Log.Information("LeagueUrls загружены для страны {@country}", country);
                }
            }
        }

        public void LoadMatches(object a)
        {
           var matches = BetExplorerParser.GetMatches(Selected, LeagueUrls.Selected);

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

           var parentId = LeagueUrls.Selected.id;

           
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

        #endregion

        public DatabaseViewModel()
        {
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
            TestCommand = new RelayCommand(Test, a=> Selected!=null);
            LoadUrlsCommand = new RelayCommand(LoadUrls, a=> Selected!=null);
            LoadMatchesCommand = new RelayCommand(LoadMatches,
                a => Selected != null && LeagueUrls.Items.Any() && LeagueUrls.Selected != null);
        }
    }
}

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

        public void Test(object a)
        {


            //
            return;
            var tstCountry = new league()
            {
                id = 1,
                name = "Austria",
                other = "",
                svgName = "",
                url = ""
            };
            var tstLeague = new leagueUrl()
            {
                id = 15,
                name = "wtf",
                other = "",
                parentId = 666,
                url = "",
                year = ""
            };

            var op = BetExplorerParser.GetMatches(tstCountry, tstLeague);

            var notCorrect = op.Where(x => !x.IsCorrect).ToList();
            foreach (var beMatch in notCorrect)
            {
                Log.Information("Not correct! {@item}", beMatch);
            }

            var onlyCorrect = op.Where(x => x.IsCorrect).ToList();

            //Добавляем новые имена команд в таблицу dbo.teamNames
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var teamNamesTable = cntx.GetTable<teamName>();
                var existingNames = teamNamesTable.Where(x => x.leagueId == 666).Select(x => x.name).ToList();

                var newNames = (from item in onlyCorrect
                    from name in item.Names
                    select name).Distinct().
                    Where(x=>!existingNames.Contains(x)).
                    ToList();

                if (newNames.Any())
                {
                    var toAddRange = newNames.Select(x => new teamName()
                        {
                            leagueId = 666,
                            name = x,
                            other = ""
                        })
                        .ToList();

                    teamNamesTable.InsertAllOnSubmit(toAddRange);
                    cntx.SubmitChanges();
                }
            }

            //Добавляем все возможные результаты в таблицу dbo.possibleResults




            /*var op = BetExplorerParser.GetLeagueUrls(Selected);

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                table.InsertAllOnSubmit(op);
                cntx.SubmitChanges();
            }*/
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
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;

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
                OnPropertyChanged("Selected");
            }
        }

        #endregion

        #region Commands
        public ICommand TestCommand { get; private set; }

        public void Test(object a)
        {
            var op = BetExplorerParser.GetLeagueUrls(Selected);

            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<leagueUrl>();
                table.InsertAllOnSubmit(op);
                cntx.SubmitChanges();
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
        }
    }
}

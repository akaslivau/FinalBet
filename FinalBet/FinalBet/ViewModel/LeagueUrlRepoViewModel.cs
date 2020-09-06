using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class LeagueUrlRepoViewModel:ViewModelBase
    {
        #region Variables

        private league _parent = null;

        private ListCollectionView _view;
        public ListCollectionView View
        {
            get
            {
                return _view;
            }
            set
            {
                if (_view == value) return;
                _view = value;
                OnPropertyChanged("View");
            }
        }


        private ObservableCollection<LeagueUrlViewModel> _items = new ObservableCollection<LeagueUrlViewModel>();
        public ObservableCollection<LeagueUrlViewModel> Items
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

        private LeagueUrlViewModel _selected;
        public LeagueUrlViewModel Selected
        {
            get
            {
                return _selected;
            }
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged("Selected");
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get
            {
                return _searchText;
            }
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged("SearchText");
            }
        }

        private bool Filter(object obj)
        {
            var item = (LeagueUrlViewModel) obj;
            if (_searchText == "#mark")
            {
                return item.Source.mark.Length > 0;
            }
            return item.Source.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) > -1;
        }

        #endregion

        #region Commands
        public ICommand SearchCommand { get; private set; }
        #endregion

        public void Update(league parent)
        {
            _parent = parent;
            if (parent == null) return;

            Application.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
            {
                Items.Clear();
            });
            
            using (var cntx = new SqlDataContext(Connection.Con))
            {
                var table = cntx.GetTable<leagueUrl>();
                var items = table.Where(x => x.parentId == _parent.id).ToList();

                foreach (var leagueUrl in items)
                {
                    var toAdd = new LeagueUrlViewModel(leagueUrl);
                    Application.Current.Dispatcher.Invoke((Action) delegate { Items.Add(toAdd); });
                }

                if (Items.Any()) Application.Current.Dispatcher.Invoke(delegate { Selected = Items[0]; });
            }
        }

        public LeagueUrlRepoViewModel()
        {
            View = new ListCollectionView(Items);

            SearchCommand = new RelayCommand(a =>
            {
                if (!string.IsNullOrEmpty(_searchText))
                {
                    View.Filter = Filter;
                }
                else
                {
                    View.Filter = null;
                }
                View.Refresh();
            });
        }
        
    }
}

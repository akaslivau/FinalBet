using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class LeagueUrlRepoViewModel:ViewModelBase
    {
        #region Variables
        private league _parent;

        private ObservableCollection<leagueUrl> _items = new ObservableCollection<leagueUrl>();
        public ObservableCollection<leagueUrl> Items
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

        private leagueUrl _selected;
        public leagueUrl Selected
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

        #endregion

        public LeagueUrlRepoViewModel(league parent)
        {
            _parent = parent;
        }

        public void Update(league parent)
        {
            _parent = parent;
            if (parent == null) return;

            Items.Clear();
            using (var cntx = new SqlDataContext(Connection.Con))
            {
                var table = cntx.GetTable<leagueUrl>();
                var items = table.Where(x => x.parentId == _parent.id).ToList();

                foreach (var leagueUrl in items)
                {
                    Items.Add(leagueUrl);
                }

                if (Items.Any()) Selected = Items[0];
            }

        }
    }
}

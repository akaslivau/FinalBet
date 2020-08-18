using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using FinalBet.Framework;

namespace FinalBet.Model
{
    public class MatchPropertyRepoViewModel:ViewModelBase
    {
        #region Fields
        private ObservableCollection<MatchPropertyViewModel> _items;
        public ObservableCollection<MatchPropertyViewModel> Items
        {
            get => _items;
            set
            {
                if (_items == value) return;
                _items = value;
                OnPropertyChanged("Items");
            }
        }

        private MatchPropertyViewModel _selected;
        public MatchPropertyViewModel Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged("Selected");
            }
        }
        #endregion

        #region Commands

        public ICommand AddCommand { get; }
        public ICommand RemoveCommand { get; }

        private void Add(object obj)
        {
            Items.Add(new MatchPropertyViewModel());
        }

        private void Remove(object obj)
        {
            Items.Remove(Selected);
        }

        #endregion

        public MatchPropertyRepoViewModel()
        {
            Items = new ObservableCollection<MatchPropertyViewModel>();

            AddCommand = new RelayCommand(Add);
            RemoveCommand = new RelayCommand(Remove, x=> Selected != null);
        }
    }
}

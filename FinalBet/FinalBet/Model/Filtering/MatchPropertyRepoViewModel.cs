using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using FinalBet.Framework;

namespace FinalBet.Model.Filtering
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
            Selected = Items[Items.Count - 1];
        }

        private void Remove(object obj)
        {
            int index = Items.IndexOf(Selected);
            Items.Remove(Selected);
            if (index > 0) Selected = Items[index - 1];

        }

        #endregion

        public List<SolveMode> GetDistinctModes()
        {
            return Items.Where(x => x.Method.NeedSolveMode).
                Select(x => x.Mode).
                Distinct().
                ToList();
        }

        public MatchPropertyRepoViewModel()
        {
            Items = new ObservableCollection<MatchPropertyViewModel>();

            AddCommand = new RelayCommand(Add);
            RemoveCommand = new RelayCommand(Remove, x=> Selected != null);
        }
    }
}

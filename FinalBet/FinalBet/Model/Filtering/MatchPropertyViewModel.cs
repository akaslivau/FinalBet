using FinalBet.Framework;

namespace FinalBet.Model.Filtering
{
    public class MatchPropertyViewModel: ViewModelBase
    {
        private MatchMethod _method;
        public MatchMethod Method
        {
            get => _method;
            set
            {
                if (_method == value) return;
                _method = value;
                OnPropertyChanged("Method");
                OnPropertyChanged("Name");
            }
        }

        public string Name => Method.Description;

        private SolveMode _mode = new SolveMode();
        public SolveMode Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                if (_mode == value) return;
                _mode = value;
                OnPropertyChanged("Mode");
            }
        }


        public MatchPropertyViewModel()
        {
            Method = Global.FilterMethods[0];
        }
    }
}

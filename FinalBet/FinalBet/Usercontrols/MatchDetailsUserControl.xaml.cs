using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.Usercontrols
{
    /// <summary>
    /// Interaction logic for MatchDetailsUserControl.xaml
    /// </summary>
    public partial class MatchDetailsUserControl : UserControl, INotifyPropertyChanged
    {
        #region DependencyProperty
        public static readonly DependencyProperty MatchIdProperty = DependencyProperty.Register(
            "MatchId", typeof(int), typeof(MatchDetailsUserControl), new PropertyMetadata(default(int), MatchIdChanged));



        private static void MatchIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MatchDetailsUserControl;
            if (control == null) return;
            if(control.MatchId<=0) return;

            control.Header = "Информация о матче, id № " + control.MatchId;
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var table = cntx.GetTable<match>();
                var match = table.Single(x => x.id == control.MatchId);

                control.HomeTeam = cntx.GetTable<teamName>().Single(x => x.id == match.homeTeamId).name;
                control.GuestTeam = cntx.GetTable<teamName>().Single(x => x.id == match.guestTeamId).name;

                var mResult = cntx.GetTable<possibleResult>().Single(x => x.id == match.matchResultId);
                control.MatchResult = mResult.value;

                if (match.firstHalfResId==null || match.secondHalfResId == null)
                {
                    control.HalfResults = "no half data";
                }
                else
                {
                    var r1 = cntx.GetTable<possibleResult>().Single(x => x.id == match.firstHalfResId);
                    var r2 = cntx.GetTable<possibleResult>().Single(x => x.id == match.secondHalfResId);
                    control.HalfResults = "(" + r1.scored + " : " + r1.missed + "; " + r2.scored + " : " + r2.missed + ")";
                }
               
                control.MatchDate = match.date.ToString("dd-MM-yyyy");
                control.Uri = match.href;
                
                //Odds
                control.Odds1x2 = "---------";
                var oddsTable = cntx.GetTable<odd>();
                var odds = oddsTable.Where(x => x.parentId == control.MatchId).ToList();
                if (odds.Any())
                {
                    var dict = odds.ToDictionary(x => x.oddType, x => x.value);
                    control.Odds1x2 = dict[OddType._1] + "\t" + dict[OddType.X] + "\t" + dict[OddType._2];
                }
            }
        }

        public int MatchId
        {
            get => (int)GetValue(MatchIdProperty);
            set => SetValue(MatchIdProperty, value);
        }

        #endregion

        #region Variables
        private string _odds1x2;
        public string Odds1x2
        {
            get
            {
                return _odds1x2;
            }
            set
            {
                if (_odds1x2 == value) return;
                _odds1x2 = value;
                OnPropertyChanged("Odds1x2");
            }
        }

        private string _header = "Выберите матч";
        public string Header
        {
            get => _header;
            set
            {
                if (_header == value) return;
                _header = value;
                OnPropertyChanged("Header");
            }
        }

        private string _homeTeam;
        public string HomeTeam
        {
            get => _homeTeam;
            set
            {
                if (_homeTeam == value) return;
                _homeTeam = value;
                OnPropertyChanged("HomeTeam");
            }
        }

        private string _guestTeam;
        public string GuestTeam
        {
            get => _guestTeam;
            set
            {
                if (_guestTeam == value) return;
                _guestTeam = value;
                OnPropertyChanged("GuestTeam");
            }
        }

        private string _matchResult;
        public string MatchResult
        {
            get => _matchResult;
            set
            {
                if (_matchResult == value) return;
                _matchResult = value;
                OnPropertyChanged("MatchResult");
            }
        }

        private string _halfResults = "";
        public string HalfResults { get { return _halfResults;} set{ _halfResults = value; OnPropertyChanged("HalfResults");}}


        private string _matchDate;
        public string MatchDate
        {
            get => _matchDate;
            set
            {
                if (_matchDate == value) return;
                _matchDate = value;
                OnPropertyChanged("MatchDate");
            }
        }

        private string _uri = "";
        public string Uri { get { return _uri;} set{ _uri = value; OnPropertyChanged("Uri");}}

        public ICommand OpenUrlCommand { get; set; }

        private void OpenUrl(object obj)
        {
            Uri baseUri = new Uri(Properties.Settings.Default.beUrl);
            Uri myUri = new Uri(baseUri, Uri);

            Process.Start(new ProcessStartInfo((myUri).AbsoluteUri));
        }
        #endregion


        public MatchDetailsUserControl()
        {
            OpenUrlCommand = new RelayCommand(OpenUrl);
            InitializeComponent();
        }

        #region INotifyPropertyChanged Members
        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This 
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,  
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (this.ThrowOnInvalidPropertyName)
                    throw new Exception(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might 
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            this.VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion // INotifyPropertyChanged Members
    }
}

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class MainWindowViewModel:ViewModelBase
    {
        #region Variables
        public DatabaseViewModel Database { get; set; }
        public RedGreenViewModel RedGreenTable { get; set; }
        #endregion

        #region Commands
        public ICommand OpenLogCommand { get; private set; }

        private void OpenLog(object a)
        {
            var fileNames = Directory.
                GetFiles(Directory.GetCurrentDirectory(), "*.txt").
                Select(Path.GetFileName)
                .Where(x => x.Contains("log")).ToList();

            if(!fileNames.Any()) return;

            if (fileNames.Count == 1)
            {
                Process.Start("notepad.exe", fileNames.First());
            }
            else
            {
                fileNames = fileNames.OrderBy(File.GetCreationTime).ToList();
                Process.Start("notepad.exe", fileNames.Last());
            }
        }

        #endregion

        public string TestString
        {
            get => Properties.Settings.Default.soccerUrl;
            set
            {
                if (Properties.Settings.Default.soccerUrl == value) return;
                Properties.Settings.Default.soccerUrl = value;
                Properties.Settings.Default.Save();
                OnPropertyChanged("TestString");
            }
        }

        public ICommand TestCommand { get; private set; }

        


        public void Test(object a)
        {

        }



        public MainWindowViewModel()
        {
            base.DisplayName = "...Ту-ду-ду-ду...";

            if (!Connection.IsSuccessful)
            {
                base.DisplayName = "ОШИБКА ПОДКЛЮЧЕНИЯ К БАЗЕ ДАННЫХ";
                return;
            }

            Database = new DatabaseViewModel();
            RedGreenTable = new RedGreenViewModel();

            //Commands
            OpenLogCommand = new RelayCommand(OpenLog);

            TestCommand = new RelayCommand(Test);



        }
    }



}

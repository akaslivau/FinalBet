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
        public DatabaseViewModel Database { get; private set; }
        public RedGreenViewModel RedGreenTable { get; private set; }
        public TestDataViewModel TestDataViewModel { get; private set; }
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
            TestDataViewModel = new TestDataViewModel();

            //Commands
            OpenLogCommand = new RelayCommand(OpenLog);

            TestCommand = new RelayCommand(Test);
        }
    }



}

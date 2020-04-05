using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FinalBet.Database;
using FinalBet.Framework;
using FinalBet.Other;
using FinalBet.Properties;
using HtmlAgilityPack;

namespace FinalBet.ViewModel
{
    public class MainWindowViewModel:ViewModelBase
    {
        public DatabaseViewModel Database { get; set; }

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
            /* Получаем полный перечень стран
             Заполняем таблицу
             Название страны, ссылка на полный список (скорее всего, нужно только russia/
             https://www.betexplorer.com/soccer/russia/
             */


            /* Получаем полный перечень ссылок для выбранной страны
             https://www.betexplorer.com/soccer/russia/premier-league-2018-2019/
             https://www.betexplorer.com/soccer/russia/premier-league/

            сохраняем его в базу данных
            Обрати внимание, для текущего сезона не ставится год
                          
             */

            /* Для выбранной ссылки (тут скорее нужно делать еще одну подтаблицу)
             * - проверяем, есть ли html для выбранной ссылки
             * - если да, то парсим его
             * - если нет, то
             * Проверяем наличие заголовка, нам нужен основной сезон (как минимум)
             * 1. <ul class="list-tabs list-tabs--secondary"> (Перечень вкладок)
               2. Надо выбрать Main (возможно другие варианты)
               3. Грузим html
               4. Ссылка по которой загружен html Добавляется в БД
             */

           /*var op=  BetExplorerParser.GetLeagueUrls(Database.Items.Single(x=>x.name=="Russia"));

           using (var cntx = new SqlDataContext(Connection.ConnectionString))
           {
               var table = cntx.GetTable<leagueUrl>();
               table.InsertAllOnSubmit(op);
               cntx.SubmitChanges();
           }*/

        }



        public MainWindowViewModel()
        {
            Database = new DatabaseViewModel();
            TestCommand = new RelayCommand(Test);

        }
    }



}

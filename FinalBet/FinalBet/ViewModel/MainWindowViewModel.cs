using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FinalBet.Framework;
using HtmlAgilityPack;

namespace FinalBet.ViewModel
{
    public class MainWindowViewModel:ViewModelBase
    {
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
             BetExplorerParser.ParseSoccerPage();
            return;
            var path = @"D:\russia_2012.html";
            var doc = new HtmlDocument();
            doc.Load(path);
        }

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

        public MainWindowViewModel()
        {
            TestCommand = new RelayCommand(Test);
        }
    }

    public static class BetExplorerParser
    {
        public static void ParseSoccerPage()
        {
            //getting html
            var path = @"D:\soccerTab.html";
            var doc = new HtmlDocument();
            doc.Load(path);

            //Начинаем парсить
            // В комментариях обозначен тэг, по которому идет разборка html
            //<ul class="list-events list-events--secondary js-divlinks" id="countries-select">
            var htmlNode = doc.GetElementbyId("countries-select");

            //Получаем название страны и ссылку
            //  < a class="list-events__item__title" href="/soccer/africa/">
            var captions = htmlNode.SelectNodes(".//a[contains(@class, 'item__title')]").Select(x=>x.InnerText).ToList();
            var links = htmlNode.SelectNodes(".//a[contains(@class, 'item__title')]").Select(x => x.GetAttributeValue("href", "default")).ToList();
            var flagNames = htmlNode.SelectNodes(".//img").
                Select(x => x.GetAttributeValue("src", "default")).
                Select(x=>x.Substring(x.LastIndexOf('/')+1)).
                ToList();

        }
    }

}

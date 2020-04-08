using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;

namespace FinalBet.Database
{
    public static class BetExplorerParser
    {
        //Заполняет таблицу dbo.leagues [название лиги, URL, flagName]
        public static void ParseSoccerPage()
        {
            //getting html from file
            var path = @"D:\soccerTab.html";
            var doc = new HtmlDocument();
            doc.Load(path);

            //getting html from url
            var url = Properties.Settings.Default.soccerUrl;
            

            //Начинаем парсить
            // В комментариях обозначен тэг, по которому идет разборка html
            //<ul class="list-events list-events--secondary js-divlinks" id="countries-select">
            var htmlNode = doc.GetElementbyId("countries-select");

            //Получаем название страны и ссылку
            //  < a class="list-events__item__title" href="/soccer/africa/">
            var captions = htmlNode.SelectNodes(".//a[contains(@class, 'item__title')]").Select(x => x.InnerText).ToList();
            var links = htmlNode.
                SelectNodes(".//a[contains(@class, 'item__title')]").
                Select(x => x.GetAttributeValue("href", "default")).
                Select(x => x.Substring(x.Substring(0, x.Length - 2).LastIndexOf('/') + 1)).
                ToList();
            var flagNames = htmlNode.SelectNodes(".//img").
                Select(x => x.GetAttributeValue("src", "default")).
                Select(x => x.Substring(x.LastIndexOf('/') + 1)).
                ToList();

            var cnt = captions.Count;

            if (links.Count != cnt || flagNames.Count != cnt)
            {
                throw new Exception("ParseSoccerPage(): количество элементов в списках различается!");
            }
            //Добавление в базу данных
            using (var cntx = new SqlDataContext(Connection.ConnectionString))
            {
                var leagueTable = cntx.GetTable<league>();

                for (int i = 0; i < cnt; i++)
                {
                    var toAdd = new league()
                    {
                        name = captions[i],
                        url = links[i],
                        svgName = flagNames[i],
                        other = ""
                    };
                    leagueTable.InsertOnSubmit(toAdd);
                }
                cntx.SubmitChanges();
            }


        }

        public static List<leagueUrl> GetLeagueUrls(league country)
        {
            var result = new List<leagueUrl>();
            var doc = new HtmlDocument();

            //getting html from file
            /*var path = @"D:\russia.html";
            doc.Load(path);*/

            //using for IDisposable.Dispose()
            var url = Properties.Settings.Default.soccerUrl + country.url;

            var web = new HtmlWeb();
            HtmlDocument webDoc;
            try
            {
                doc = web.Load(url);
            }
            catch (Exception ex)
            {
                
            }
            
            //Начинаем парсить
            // В комментариях обозначен тэг, по которому идет разборка html
            //< table class="table-main js-tablebanner-t">
            var htmlNode = doc.DocumentNode.SelectSingleNode("//table[@class='table-main js-tablebanner-t']");


            //Тут просто <tbody>, но нам нужны только те, которые содержат
            //<th class="h-text-left">
            var yearNodes = htmlNode.SelectNodes(".//tbody").
                Where(x=>x.Descendants("th").Any()).
                ToList();

            foreach (var yearNode in yearNodes)
            {
                //<th class="h-text-left">2019/2020</th>
                string year = yearNode.SelectSingleNode(".//th[@class='h-text-left']").InnerText;

                //<a href="https://www.betexplorer.com/soccer/russia/premier-league/">Premier League</a>
                var names = yearNode.SelectNodes(".//a").Select(x=>x.InnerText).ToList();
                var urls = yearNode.SelectNodes(".//a").
                    Select(x => x.GetAttributeValue("href", "default")).
                    Select(x => x.Substring(x.Substring(0, x.Length - 2).LastIndexOf('/') + 1)).
                    ToList();

                if(!names.Any() || (names.Count != urls.Count))
                    throw new Exception("GetLeagueUrls: Ошибка парсинга. В выбранном <tbody> нет лиг!");

                for (int i = 0; i < names.Count; i++)
                {
                    var toAdd = new leagueUrl()
                    {
                        parentId = country.id,
                        name = names[i],
                        url = urls[i],
                        year = year,
                        other = ""
                    };
                    result.Add(toAdd);
                }
            }
            
            return result;
        }
    }
}

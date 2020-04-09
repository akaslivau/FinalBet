using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Other;
using HtmlAgilityPack;
using Serilog;
using Serilog.Core;

namespace FinalBet.Database
{
    public static class BetExplorerParser
    {
        public const string RESULTS = "results/";
        public const string NO_TABS_TAG = "NO_TABS";
        public const char BE_TEAMS_DELIMITER = '-';

        //Заполняет таблицу dbo.leagues [название лиги, URL, flagName]
        public static void ParseSoccerPage()
        {
            //getting html from file
            /*var path = @"D:\soccerTab.html";
            var doc = new HtmlDocument();
            doc.Load(path);*/

            //getting html from url
            var url = Properties.Settings.Default.soccerUrl;
            var doc = new HtmlDocument();
            
            var web = new HtmlWeb();
            try
            {
                doc = web.Load(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ParseSoccerPage can't load html");
            }



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

        //Возвращает список URL с сезонами для выбранной лиги
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
            try
            {
                doc = web.Load(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetLeagueUrls can't load html");
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

        //Возвращает список матчей для выбранной ссылки
        //В случае, если по указанной ссылке находятся несколько Tab-вкладок
        //Например, Russia-2013, [Main, Regelation],
        //То будут возвращены все матчи
        public static List<BeMatch> GetMatches(league country, leagueUrl leagueUrl)
        {
            var result = new List<BeMatch>();

            var doc = new HtmlDocument();

            //using for IDisposable.Dispose()
            /*var url = Properties.Settings.Default.soccerUrl + country.url + leagueUrl.url + RESULTS;

            var web = new HtmlWeb();
            try
            {
                doc = web.Load(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetMatches can't load html");
            }*/


            var path = @"D:\Only_res.html";
            doc.Load(path);

            //Проверяем, есть ли несколько вкладок
            //<ul class="list-tabs list-tabs--secondary"....
            var isSingleTab = doc.DocumentNode.SelectSingleNode(".//ul[contains(@class, 'list-tabs--secondary')]") == null;

            //Если одна вкладка, то возвращаем список матчей из загруженного документа
            if (isSingleTab)
            {
               return GetMatches(doc, NO_TABS_TAG);
            }
            else
            {
                //Иначе получаем список новых ссылок и их названий
                var tabListNode = doc.DocumentNode.SelectSingleNode(".//ul[contains(@class, 'list-tabs--secondary')]");

                //<li class="list-tabs__item">
                var urls = tabListNode.SelectNodes(".//li[@class='list-tabs__item']").
                    Select(x=>x.SelectSingleNode(".//a")).
                    ToList();

                for (int i = 0; i < urls.Count; i++)
                {
                    var href = urls[i].GetAttributeValue("href", "default");
                    var tag = urls[i].InnerText;

                    //Загружаем документ по полученным ссылкам
                    var web2 = new HtmlWeb();
                    var doc2 = new HtmlDocument();
                    try
                    {
                        doc2 = web2.Load(href);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "GetMatches can't load html");
                    }

                    //Распарсиваем список матчей и добавляем к результату
                    var matches = GetMatches(doc2, tag);
                    result.AddRange(matches);
                }
            }
            return result;
        }

        private static List<BeMatch> GetMatches(HtmlDocument doc, string tag)
        {
            var result = new List<BeMatch>();

            //< table class="table-main h-mb15
            var tableNode = doc.DocumentNode.SelectSingleNode(".//table[contains(@class, 'table-main h-mb15')]");

            //<tr>
            //  <td class="h-text-left">
            //      <a href="https://www.betexplorer.com/soccer/russia/premier-league-2013-2014/amkar-krasnodar/xpAWzhYl/" class="in-match">
            //          <span>Amkar</span> - <span>Krasnodar</span>
            //      </a>
            //  </td>
            //  <td class="h-text-center">
            //      <a href="https://www.betexplorer.com/soccer/russia/premier-league-2013-2014/amkar-krasnodar/xpAWzhYl/">
            //          2:2
            //      </a>
            //  </td>
            //  <td class="table-main__odds">3.94</td>
            //  <td class="table-main__odds colored">
            //      <span><span><span>3.41</span></span></span>
            //  </td>
            //  <td class="table-main__odds">1.87</td>
            //  <td class="h-text-right h-text-no-wrap">15.05.2014</td>
            //</tr>
            var trNodes = tableNode.SelectNodes(".//tr").ToList();
            foreach (var tr in trNodes)
            {
                if(tr.InnerText.Contains("Round") || tr.InnerText.Length < 10) continue; //<tr><th class="h-text-left" colspan="2">1. Round</th><th class="h-text-center">1</th><th class="h-text-center">X</th><th class="h-text-center">2</th><th>&nbsp;</th></tr>

                var names = tr.SelectSingleNode(".//td[@class='h-text-left']");
                if (names == null)
                {
                    Log.Warning("GetMatches. Tr node parse. Null td node with names. " + tr.InnerText);
                    continue;
                }

                var teams = names.SelectNodes(".//span").Select(x => x.InnerText).ToList();
                var matchHref = names.SelectSingleNode(".//a[@class='in-match']").
                                            GetAttributeValue("href", "default");

                var finalScore = tr.SelectSingleNode(".//td[@class='h-text-center']").InnerText.Trim();

                var odds = tr.SelectNodes(".//td[contains(@class, 'table-main__odds')]").
                    Select(x => x.InnerText.Trim())
                    .ToList();

                var date = tr.SelectSingleNode(".//td[contains(@class, 'h-text-right')]").InnerText;
                
                var toAdd = new BeMatch(teams, matchHref, finalScore, odds, date);
                result.Add(toAdd);
            }

            return result;
        }


        public static void ParseMatchResult(string matchResult, out bool isCorrect, out int scored, out int missed)
        {
            isCorrect = false;
            scored = -1;
            missed = -1;

        }

    }

    public class BeMatch
    {
        public List<string> Names { get; private set; }
        public string Href { get; private set; }
        public string FinalScore { get; private set; }
        public List<string> Odds { get; private set; }
        public string Date { get; private set; }

        public bool IsCorrect { get; private set; }

        public BeMatch(List<string> names, string href, string finalScore, List<string> odds, string date)
        {
            Names = new List<string>();
            Odds = new List<string>();

            Href = href;
            FinalScore = finalScore;
            Date = date;
            
            foreach (var name in names)
            {
                Names.Add(name);
            }

            foreach (var odd in odds)
            {
                Odds.Add(odd);
            }

            IsCorrect = GetIsCorrect();
        }

        private bool GetIsCorrect()
        {
            if (Names.Count != 2) return false;
            if (Names.Any(x => x.Length < 1)) return false;

            if (string.IsNullOrEmpty(Href)) return false;
            if (string.IsNullOrEmpty(FinalScore)) return false;

            if (Odds.Any(string.IsNullOrEmpty)) return false;
            if (Odds.Count != 3) return false;

            if (string.IsNullOrEmpty(Date)) return false;
            DateTime dt;
            if (!DateTime.TryParse(Date, out dt)) return false;

            return true;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using Serilog;
using System.IO.Compression;
using System.Linq.Expressions;
using FinalBet.Model;
using FinalBet.Properties;
using Serilog.Core;

namespace FinalBet.Database
{
    public static class BetExplorerParser
    {
        public const string RESULTS = "results/";
        public const string NO_TABS_TAG = "NO_TABS";
        public const char BE_TEAMS_DELIMITER = '-';
        public const char BE_SCORE_DELIMITER = ':';

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
                        svgName = flagNames[i]
                    };
                    leagueTable.InsertOnSubmit(toAdd);
                }
                cntx.SubmitChanges();
            }


        }


        public static async Task<string> GetLeagueUrlsHtml(league country)
        {
            var doc = new HtmlDocument();
            bool hasError = false;

            //using for IDisposable.Dispose()
            var url = Properties.Settings.Default.soccerUrl + country.url;

            var web = new HtmlWeb();
            try
            {
                doc = await web.LoadFromWebAsync(url);
            }
            catch (Exception ex)
            {
                hasError = true;
                Log.Error(ex, "GetLeagueUrlsHtml can't load html");
                Global.Current.Errors++;
            }


            return hasError ? "" : doc.DocumentNode.InnerHtml;
        }
        
        
        //Возвращает список URL с сезонами для выбранной лиги
        public static List<leagueUrl> GetLeagueUrls(string html, int countryId)
        {
            var result = new List<leagueUrl>();
            if (string.IsNullOrEmpty(html)) return result;
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
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
                        parentId = countryId,
                        name = names[i],
                        url = urls[i],
                        year = year,
                        mark = ""
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
        public static async Task<List<BeMatch>> GetMatches(league country, leagueUrl leagueUrl)
        {
            var doc = new HtmlDocument();

            //Проверяем, есть ли загруженный html в архиве
            var zipPath = GetZipPath(country, leagueUrl);  

            var isHtmlExists = File.Exists(zipPath);
            if (isHtmlExists)
            {
                using (ZipArchive zip = ZipFile.OpenRead(zipPath))
                {
                    if (zip.Entries.All(x => x.Name != Settings.Default.zipMainFilename))
                    {
                        Log.Fatal("В zip архиве нет файла " + Settings.Default.zipMainFilename);
                        Global.Current.Errors++;
                        throw new Exception();
                    }

                    var entry = zip.Entries.Single(x => x.Name == Settings.Default.zipMainFilename);
                    entry.ExtractToFile(Settings.Default.zipMainFilename, true);

                    //Загружаем html из извлеченного файла
                    doc.Load(Settings.Default.zipMainFilename);
                }
            }
            else //Загружаем из интернета
            {
                //using for IDisposable.Dispose()
                var url = Properties.Settings.Default.soccerUrl + country.url + leagueUrl.url + RESULTS;

                var web = new HtmlWeb();
                try
                {
                    doc = await web.LoadFromWebAsync(url);
                    //Zipping HTML
                    File.WriteAllText(Properties.Settings.Default.tmpHtmlFile, doc.DocumentNode.InnerHtml);
                    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(Properties.Settings.Default.tmpHtmlFile,
                            Settings.Default.zipMainFilename,
                            CompressionLevel.Optimal);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "GetMatches can't load html");
                    Global.Current.Errors++;
                }
            }
            
            //Проверяем, есть ли несколько вкладок
            //<ul class="list-tabs list-tabs--secondary"....
            var isSingleTab = doc.DocumentNode.SelectSingleNode(".//ul[contains(@class, 'list-tabs--secondary')]") == null;

            //Если одна вкладка, то возвращаем список матчей из загруженного документа
            if (isSingleTab)
            {
               return GetMatches(doc, NO_TABS_TAG);
            }

            //Иначе получаем список новых ссылок и их названий
            //И тут может быть совсем по ебанутому, когда ТАБы идут в 2 ряда...или 3...
            //Например чемпионат Европы, Кубок Азии, Кубок Мира с тремя уровнями и т.д.
            var tabListNode = doc.DocumentNode.
                SelectNodes(".//ul[contains(@class, 'list-tabs--secondary')]")
                .ToList();

            // ***********************************************************   ОДИН РЯД ТАБОВ  **************************
            //Значит один ряд Табов и просто его парсим
            if (tabListNode.Count == 1)
            {
                var urls = tabListNode.First().
                    SelectNodes(".//li[@class='list-tabs__item']")
                    .Select(x => x.SelectSingleNode(".//a")).
                    Select(x => new StageUrl(x, "", country, leagueUrl)).ToList();

                return GetMatchesFromUrlsList(urls);
            }
            //Иначе у нас больше одного ряда
            //Находим главный div  //< div class="h-mb15">
            var mainDiv = doc.DocumentNode.SelectSingleNode(".//div[@class='h-mb15']");
            //Получаем первый ряд
            var main_li_ids = tabListNode.First().
                SelectNodes(".//li[contains(@id, 'sm-a')]").
                Select(x=>x.GetAttributeValue("id", default(string))).
                Where(x=>!string.IsNullOrEmpty(x)).
                ToList();

            //<div class="box-overflow">
            var div_s = mainDiv.SelectNodes(".//div[@class='box-overflow']")
                .ToList();

            // ***********************************************************   ДВА РЯДА ТАБОВ  **************************
            //Это в теории значит, что у нас всего 2 ряда табов, и можно их так херануть
            if (div_s.Count == (main_li_ids.Count + 1))
            {
                var itemsCaseTwoTabs = tabListNode.First().SelectNodes(".//li[contains(@id, 'sm-a')]")
                    .Select(
                        x =>
                            new
                            {
                                mainTag = x.InnerText.Trim(),
                                id = x.GetAttributeValue("id", default(string)).Replace("a-","")
                            }).ToList();

                var stageUrlsTwoTabs = new List<StageUrl>();
                foreach (var item in itemsCaseTwoTabs)
                {
                    var div_id_node = mainDiv.SelectSingleNode(".//div[@id='"+item.id+"']");

                    var toAdd = div_id_node.
                        SelectNodes(".//li[@class='list-tabs__item']")
                        .Select(x => x.SelectSingleNode(".//a")).
                        Select(x => new StageUrl(x, item.mainTag, country, leagueUrl)).ToList();

                    stageUrlsTwoTabs.AddRange(toAdd);
                }
                return GetMatchesFromUrlsList(stageUrlsTwoTabs);
            }

            // ***********************************************************   ТРИ РЯДА ТАБОВ  **************************
            //АХАХАХХАХА-АХАХАХАХАХ
            var itemsCaseThreeTabs = tabListNode.First().SelectNodes(".//li[contains(@id, 'sm-a')]")
                .Select(
                    x =>
                        new
                        {
                            mainTag = x.InnerText.Trim(),
                            id = x.GetAttributeValue("id", default(string)).Replace("a-", "")
                        }).ToList();

            var stageUrlsThreeTabs = new List<StageUrl>();
            foreach (var item in itemsCaseThreeTabs)
            {
                var div_id_node = mainDiv.SelectSingleNode(".//div[@id='" + item.id + "']");

                //Значит тут реально только 2 ряда табов
                if (div_id_node != null)
                {
                    var toAdd = div_id_node.
                        SelectNodes(".//li[@class='list-tabs__item']")
                        .Select(x => x.SelectSingleNode(".//a")).
                        Select(x => new StageUrl(x, item.mainTag, country, leagueUrl)).ToList();

                    stageUrlsThreeTabs.AddRange(toAdd);
                }
                else
                {
                    var shortId = item.id.Substring(0, 4);
                    var div_lvl2_node = mainDiv.SelectSingleNode(".//div[@id='" + shortId + "']");
                    if (div_lvl2_node != null)
                    {
                        var lvl_two_tabs = div_lvl2_node.SelectNodes(".//li[contains(@id, 'sm-a')]")
                            .Select(
                                x =>
                                    new
                                    {
                                        mainTag = x.InnerText.Trim(),
                                        id = x.GetAttributeValue("id", default(string)).Replace("a-", "")
                                    }).ToList();

                        foreach (var lvlTwoTab in lvl_two_tabs)
                        {
                            var div_lvl3_node = mainDiv.SelectSingleNode(".//div[@id='" + lvlTwoTab.id + "']");
                            if (div_lvl3_node != null)
                            {
                                var toAdd = div_lvl3_node.
                                    SelectNodes(".//li[@class='list-tabs__item']")
                                    .Select(x => x.SelectSingleNode(".//a")).
                                    Select(x => new StageUrl(x, item.mainTag + " - " + lvlTwoTab.mainTag, country, leagueUrl)).ToList();

                                stageUrlsThreeTabs.AddRange(toAdd);
                            }
                        }
                    }
                }
            }
            return GetMatchesFromUrlsList(stageUrlsThreeTabs);

            throw new Exception("Impossible!");
        }

        private static List<BeMatch> GetMatchesFromUrlsList(List<StageUrl> urls)
        {
            var result = new List<BeMatch>();
            for (int i = 0; i < urls.Count; i++)
            {
                if (!urls[i].HasStage) continue;

                //Загружаем документ по полученным ссылкам
                var doc2 = new HtmlDocument();
                var stageFileName = urls[i].Stage + ".html";

                var isStageHtmlExists = false;
                using (ZipArchive zip = ZipFile.OpenRead(urls[i].ZipPath))
                {
                    isStageHtmlExists = zip.Entries.Any(x => x.Name == stageFileName);
                }

                if (isStageHtmlExists)
                {
                    using (ZipArchive zip = ZipFile.OpenRead(urls[i].ZipPath))
                    {
                        var entry = zip.Entries.Single(x => x.Name == stageFileName);
                        entry.ExtractToFile("stage" + i + ".html", true);
                    }

                    //Загружаем html из извлеченного файла
                    doc2.Load("stage" + i + ".html");
                }
                else
                {
                    var web2 = new HtmlWeb();
                    doc2 = web2.Load(urls[i].WebUrl);
                    File.WriteAllText("stage" + i + ".html", doc2.DocumentNode.InnerHtml);

                    using (ZipArchive zip = ZipFile.Open(urls[i].ZipPath, ZipArchiveMode.Update))
                    {
                        zip.CreateEntryFromFile("stage" + i + ".html", stageFileName);
                    }
                }

                //Распарсиваем список матчей и добавляем к результату
                var matches = GetMatches(doc2, urls[i].GetFinalTag());
                result.AddRange(matches);
            }
            return result;
        }

        private static List<BeMatch> GetMatches(HtmlDocument doc, string tag)
        {
            var result = new List<BeMatch>();

            //< table class="table-main h-mb15
            var tableNode = doc.DocumentNode.SelectSingleNode(".//table[contains(@class, 'table-main h-mb15')]");
            
            //Это вариант при ручнос сохранении:)
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

            //Это вариант при машинном сохранении, видимо, не успевают скрипты отработать
            //<tr>
            //  < td class="h-text-left">
            //      <a href = "/soccer/austria/tipico-bundesliga-2017-2018/admira-st-polten/nP2TjLD6/" class="in-match">
            //          <span>Admira</span> - <span><strong>St.Polten</strong></span>
            //      </a>
            //  </td>
            //  <td class="h-text-center">
            //      <a href = "/soccer/austria/tipico-bundesliga-2017-2018/admira-st-polten/nP2TjLD6/" > 0:2</a>
            //  </td>
            //  <td class="table-main__odds" data-odd="1.98"></td>
            //  <td class="table-main__odds" data-odd="3.67"></td>
            //  <td class="table-main__odds colored"><span><span><span data-odd="3.42"></span></span></span></td>
            //  <td class="h-text-right h-text-no-wrap">27.05.2018</td>
            //</tr>

            var trNodes = tableNode.SelectNodes(".//tr").ToList();
            foreach (var tr in trNodes)
            {
                if(tr.InnerText.Contains("Round")) continue; //<tr><th class="h-text-left" colspan="2">1. Round</th><th class="h-text-center">1</th><th class="h-text-center">X</th><th class="h-text-center">2</th><th>&nbsp;</th></tr>

                var names = tr.SelectSingleNode(".//td[@class='h-text-left']");
                if (names == null)
                {
                    continue;
                }

                var teams = names.SelectNodes(".//span").Select(x => x.InnerText).ToList();
                var matchHref = names.SelectSingleNode(".//a[@class='in-match']").
                                            GetAttributeValue("href", "default");

                var finalScore = tr.SelectSingleNode(".//td[@class='h-text-center']").InnerText.Trim();

                //odds не парсим, так как все равно их потом подгружать
              
                var date = tr.SelectSingleNode(".//td[contains(@class, 'h-text-right')]").InnerText;
                
                var toAdd = new BeMatch(teams, matchHref, finalScore, date, tag);
                result.Add(toAdd);
            }

            return result;
        }
        
        public static void ParseMatchResult(string matchResult, out bool isCorrect, out int scored, out int missed)
        {
            isCorrect = false;
            scored = -1;
            missed = -1;

            //Contains any letter == notCorrect
            bool hasAnyLetter = matchResult.Any(char.IsLetter);
            if (hasAnyLetter) return;

            //Parsing
            var pos = matchResult.IndexOf(BE_SCORE_DELIMITER);
            if (pos < 0) return;

            var strScore = matchResult.Substring(0, pos).Trim();
            var strMissed = matchResult.Substring(pos + 1).Trim();

            if (!int.TryParse(strScore, out scored)) return;
            if (!int.TryParse(strMissed, out missed)) return;

            isCorrect = true;
        }

        public static string GetZipPath(league country, leagueUrl leagueUrl)
        {
            var zipFileName = country.name + "_urlId__" + leagueUrl.id + ".zip";
            return Settings.Default.zipFolder + zipFileName;
        }

    }

    public class BeMatch
    {
        public List<string> Names { get; private set; }
        public string Href { get; private set; }
        public string FinalScore { get; private set; }
        public string Date { get; private set; }
        public string Tag { get; private set; }

        public bool IsCorrect { get; private set; }

        public BeMatch(List<string> names, string href, string finalScore, string date, string tag)
        {
            Names = new List<string>();

            Href = href;
            FinalScore = finalScore;
            Date = date;
            Tag = tag;
            
            foreach (var name in names)
            {
                Names.Add(name);
            }

            IsCorrect = GetIsCorrect();
        }

        private bool GetIsCorrect()
        {
            if (Names.Count != 2) return false;
            if (Names.Any(x => x.Length < 1)) return false;

            if (string.IsNullOrEmpty(Href)) return false;
            if (string.IsNullOrEmpty(FinalScore)) return false;

            if (string.IsNullOrEmpty(Date)) return false;
            DateTime dt;
            if (!DateTime.TryParse(Date, out dt)) return false;

            return true;
        }
    }

    public class StageUrl
    {
        //<a href="https://www.betexplorer.com/soccer/russia/premier-league-2013-2014/results/?stage=baGxDORM"
        #region Variables
        public string Href { get; private set; }
        public string SelfTag { get; private set; }
        public bool HasStage { get; private set; }
        public string Stage { get; private set; }
        public HtmlNode Node { get; private set; }
        public string WebUrl { get; private set; }
        public string ZipPath { get; private set; }
        #endregion

        public string OuterTag { get; private set; }

        public string GetFinalTag()
        {
            return string.IsNullOrEmpty(OuterTag) ? SelfTag : OuterTag + "-" + SelfTag;
        }
        
        public StageUrl(HtmlNode node, string outerTag, league country, leagueUrl leagueUrl)
        {
            ZipPath = BetExplorerParser.GetZipPath(country, leagueUrl);
            
            Node = node;
            OuterTag = outerTag;

            SelfTag = node.InnerText;
            Href = node.GetAttributeValue("href", "");
            HasStage = !string.IsNullOrEmpty(Href) && Href.Contains("?stage=");

            if (HasStage)
            {
                var stagePos = Href.IndexOf("?stage=") + "?stage=".Length;
                Stage = Href.Substring(stagePos, Href.Length - stagePos);
            }

            WebUrl = Settings.Default.soccerUrl + country.url + leagueUrl.url + BetExplorerParser.RESULTS + Href;
        }
    }
}

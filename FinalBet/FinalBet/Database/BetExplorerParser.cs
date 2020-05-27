using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Serilog;
using System.IO.Compression;
using FinalBet.Model;
using FinalBet.Properties;

namespace FinalBet.Database
{
    public static class BetExplorerParser
    {
        public const string RESULTS = "results/";
        public const string NO_TABS_TAG = "NO_TABS";
        public const char BE_SCORE_DELIMITER = ':';

        /* ОПИСАНИЕ ИЕРАРХИИ ТАБЛИЦ (СМОТРИ ДЛЯ НАГЛЯДНОСТИ Sql.dbml)
         * 0. dbo.leagues -  страны (континенты и пр.)
         * 1. dbo.leagueUrls - список Url для всех стран (parentId <== leagues.id)
         * 2а. dbo.teamNames - имена команд (leagueId <== leagueUrl.id)
         * 2b. dbo.matches - список матчей (parentId <== leagueUrl.id)
         * 3a. dbo.results - результаты матчей (parentId <== match.id, resultId <== possibleResult.id)
         * 3b. dbo.odds - коэффициенты (parentId <== match.id)
         *
         * dbo.matchTags - содержит список тэгов, полученных при парсинге матчей (NO_TABS по умолчанию)
         * dbo.possibleResults - содержит все уникальные результаты матчей
         * dbo.solveMode - перечисление возможных режимов для CodeSelector
         * dbo.leagueMarks - перечисление для маркировки leagueUrl (Основной турнир, Кубок и т.д.)
         * dbo.parsedResults - вспомогательная таблица для парсинга
         */

        #region 0. dbo.leagues

        /// <summary>
        /// Заполняет таблицу dbo.leagues [название лиги, URL, flagName]. Однократный запуск
        /// </summary>
        public static void ParseSoccerPage()
        {
            //getting html from url
            var url = Settings.Default.soccerUrl;
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
                if (leagueTable.Any())
                {
                    Log.Warning("Таблица dbo.leagues не пустая. Операция преркащена.");
                    Global.Current.Warnings++;
                    return;
                }

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
        #endregion

        #region 1. dbo.leagueUrls
        //Возвращает список leagueUrl для выбранной страны
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
                Where(x => x.Descendants("th").Any()).
                ToList();

            foreach (var yearNode in yearNodes)
            {
                //<th class="h-text-left">2019/2020</th>
                string year = yearNode.SelectSingleNode(".//th[@class='h-text-left']").InnerText;

                //<a href="https://www.betexplorer.com/soccer/russia/premier-league/">Premier League</a>
                var names = yearNode.SelectNodes(".//a").Select(x => x.InnerText).ToList();
                var urls = yearNode.SelectNodes(".//a").
                    Select(x => x.GetAttributeValue("href", "default")).
                    Select(x => x.Substring(x.Substring(0, x.Length - 2).LastIndexOf('/') + 1)).
                    ToList();

                if (!names.Any() || (names.Count != urls.Count))
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

        //Получает html для последующего заполнения dbo.leagueUrls
        public static async Task<string> GetLeagueUrlsHtml(league country)
        {
            var doc = new HtmlDocument();
            bool hasError = false;

            //using for IDisposable.Dispose()
            var url = Settings.Default.soccerUrl + country.url;

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
        #endregion

        #region 2. dbo.matches
        //Возвращает список BeMatch(вспомогательный класс) для выбранной ссылки
        //В случае, если по указанной ссылке находятся несколько Tab-вкладок
        //Например, Russia-2013, [Main, Regelation],
        //То будут возвращены все матчи с соответствующими matchTag
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
                var url = Settings.Default.soccerUrl + country.url + leagueUrl.url + RESULTS;

                var web = new HtmlWeb();
                try
                {
                    doc = await web.LoadFromWebAsync(url);
                    //Zipping HTML
                    File.WriteAllText(Settings.Default.tmpHtmlFile, doc.DocumentNode.InnerHtml);
                    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(Settings.Default.tmpHtmlFile,
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
            var mainLiIds = tabListNode.First().
                SelectNodes(".//li[contains(@id, 'sm-a')]").
                Select(x => x.GetAttributeValue("id", default(string))).
                Where(x => !String.IsNullOrEmpty(x)).
                ToList();

            //<div class="box-overflow">
            var divNodes = mainDiv.SelectNodes(".//div[@class='box-overflow']")
                .ToList();

            // ***********************************************************   ДВА РЯДА ТАБОВ  **************************
            //Это в теории значит, что у нас всего 2 ряда табов, и можно их так херануть
            if (divNodes.Count == (mainLiIds.Count + 1))
            {
                var itemsCaseTwoTabs = tabListNode.First().SelectNodes(".//li[contains(@id, 'sm-a')]")
                    .Select(
                        x =>
                            new
                            {
                                mainTag = x.InnerText.Trim(),
                                id = x.GetAttributeValue("id", default(string)).Replace("a-", "")
                            }).ToList();

                var stageUrlsTwoTabs = new List<StageUrl>();
                foreach (var item in itemsCaseTwoTabs)
                {
                    var divIdNode = mainDiv.SelectSingleNode(".//div[@id='" + item.id + "']");

                    var toAdd = divIdNode.
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
                var divIdNode = mainDiv.SelectSingleNode(".//div[@id='" + item.id + "']");

                //Значит тут реально только 2 ряда табов
                if (divIdNode != null)
                {
                    var toAdd = divIdNode.
                        SelectNodes(".//li[@class='list-tabs__item']")
                        .Select(x => x.SelectSingleNode(".//a")).
                        Select(x => new StageUrl(x, item.mainTag, country, leagueUrl)).ToList();

                    stageUrlsThreeTabs.AddRange(toAdd);
                }
                else
                {
                    var shortId = item.id.Substring(0, 4);
                    var divLvl2Node = mainDiv.SelectSingleNode(".//div[@id='" + shortId + "']");
                    if (divLvl2Node != null)
                    {
                        var lvlTwoTabs = divLvl2Node.SelectNodes(".//li[contains(@id, 'sm-a')]")
                            .Select(
                                x =>
                                    new
                                    {
                                        mainTag = x.InnerText.Trim(),
                                        id = x.GetAttributeValue("id", default(string)).Replace("a-", "")
                                    }).ToList();

                        foreach (var lvlTwoTab in lvlTwoTabs)
                        {
                            var divLvl3Node = mainDiv.SelectSingleNode(".//div[@id='" + lvlTwoTab.id + "']");
                            if (divLvl3Node != null)
                            {
                                var toAdd = divLvl3Node.
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
        }

        //Разборщик html
        private static List<BeMatch> GetMatchesFromUrlsList(List<StageUrl> urls)
        {
            var result = new List<BeMatch>();
            for (int i = 0; i < urls.Count; i++)
            {
                if (!urls[i].HasStage) continue;

                //Загружаем документ по полученным ссылкам
                var doc2 = new HtmlDocument();
                var stageFileName = urls[i].Stage + ".html";

                bool isStageHtmlExists;
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

        //Разборщик html
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
                if (tr.InnerText.Contains("Round")) continue; //<tr><th class="h-text-left" colspan="2">1. Round</th><th class="h-text-center">1</th><th class="h-text-center">X</th><th class="h-text-center">2</th><th>&nbsp;</th></tr>

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
        #endregion

        #region 3a. dbo.results (First, Second halfs)
        private static async Task<string> GetHalfResultsHtml(match match)
        {
            var web = new HtmlWeb();
            var url = Settings.Default.beUrl + match.href.Substring(1, match.href.Length - 1);
            try
            {
                var doc = await web.LoadFromWebAsync(url);
                return doc.DocumentNode.InnerHtml;
            }
            catch (Exception e)
            {
                Log.Fatal(e, "GetMatchDetailHtml-Task");
                Global.Current.Errors++;
                return string.Empty;
            }
        }

        private static MatchDetail GetHalfsResults(string html, int matchId)
        {
            #region Html

            /*< li class="list-details__item">
    <figure class="list-details__item__team"><div>
    <a href = "/soccer/team/russia/hrgrswHh/" >< img src="/res/images/team-logo/ny81SOoh-WQE2Dwu6.png" alt="Russia">
    </a>
    </div></figure>
    <h2 class="list-details__item__title"><a href = "/soccer/team/russia/hrgrswHh/" > Russia </ a ></ h2 >
    </ li >
    <li class="list-details__item">

< p class="list-details__item__date" id="match-date" data-dt="7,7,2018,20,00"></p>
    <p class="list-details__item__score" id="js-score">2:3</p>
    <h2 class="list-details__item__eventstage" id="js-eventstage">After Penalties</h2>
    <h2 class="list-details__item__partial" id="js-partial">(1:1, 0:0, 1:1, 3:4)</h2></li>
    <li class="list-details__item">
    <figure class="list-details__item__team"><div>
    <a href = "/soccer/team/croatia/K8aznggo/" >< img src="/res/images/team-logo/dWUpPN93-pEdsWetM.png" alt="Croatia">
    </a>
    </div></figure>
    <h2 class="list-details__item__title"><a href = "/soccer/team/croatia/K8aznggo/" > Croatia </ a ></ h2 >
    </ li >
    */


            #endregion
            if (string.IsNullOrEmpty(html)) return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            //< ul class="list-details">
            var node = doc.DocumentNode.SelectSingleNode(".//ul[@class='list-details']");

            //Final score
            //<p class="list-details__item__score" id="js-score">2:2</p>

            //< h2 class="list-details__item__partial" id="js-partial">(1:1, 0:0, 1:1, 3:4)</h2></li>
            var scoresString = node.SelectSingleNode(".//h2[@class='list-details__item__partial']").InnerText;
            bool hasScores = scoresString.Any(x => x == ':');
            if (!hasScores) return null;


            var scores = scoresString.Split(',', '(', ')').
                Where(x => !String.IsNullOrEmpty(x)).
                Select(x => x.Trim()).ToList();
            if (scores.Count < 2) return null;

            return new MatchDetail(scores[0], scores[1], matchId);
        }

        public static async Task<MatchDetail> GetHalfsResults(match match)
        {
            var html = await GetHalfResultsHtml(match);
            return GetHalfsResults(html, match.id);
        }
        #endregion

        #region 3b. dbo.odds
        public static async Task<List<odd>> GetMatchOdds(match match, BeOddLoadMode loadMode)
        {
            var html = await GetOddHtml(match, loadMode);
            return GetOddsFromHtml(match.id, html, loadMode);
        }

        private static List<odd> GetOddsFromHtml(int parentId, string html, BeOddLoadMode oddParseLoadMode)
        {
            if (string.IsNullOrEmpty(html)) return new List<odd>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (oddParseLoadMode == BeOddLoadMode.OU) return GetTotalOdds(doc, parentId);
            if (oddParseLoadMode == BeOddLoadMode._1X2) return Get1X2Odds(doc, parentId);
            if (oddParseLoadMode == BeOddLoadMode.AH) return GetAhOdds(doc, parentId);
            if (oddParseLoadMode == BeOddLoadMode.BTS) return GetBtsOdds(doc, parentId);

            throw new NotImplementedException("Ty 4o, pes?");
        }

        private static async Task<string> GetOddHtml(string href, BeOddLoadMode oddLoadMode)
        {
            //PowerShell Web Request
            /*Invoke - WebRequest - Uri "https://www.betexplorer.com/match-odds/WEqpywFK/1/ou/" - Headers @{
                "method" = "GET"
                "authority" = "www.betexplorer.com"
                "scheme" = "https"
                "path" = "/match-odds/WEqpywFK/1/ou/"
                "accept" = "application/json, text/javascript, #1#*; q=0.01"
                "user-agent" = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36"
                "x-requested-with" = "XMLHttpRequest"
                "sec-fetch-site" = "same-origin"
                "sec-fetch-mode" = "cors"
                "sec-fetch-dest" = "empty"
                "referer" = "https://www.betexplorer.com/soccer/world/world-cup-2018/russia-croatia/WEqpywFK/"
                "accept-encoding" = "gzip, deflate, br"
                "accept-language" = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7"
                "cookie" = "_ga=GA1.2.11540757.1585916447; _hjid=b91dbff5-1411-47cc-bb61-195432b8ea14; my_timezone=%2B1; my_cookie_id=110484032; my_cookie_hash=3822e9f84dd7c28bc9f5bab57f88526f; _gid=GA1.2.1671225560.1588507164; js_cookie=1; _session_UA-191939-1=true; _hjIncludedInSample=1; page_cached=1; widget_pageViewCount=6"
            }
            -OutFile D:\out_wr.txt*/

            var matchId = href.Split('/').Last(x => !String.IsNullOrEmpty(x));
            string url = "https://www.betexplorer.com/match-odds/" + matchId + "/1/" + oddLoadMode + "/";

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Accept = "application/json, text/javascript, #1#*; q=0.01";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36";
            request.Referer = Settings.Default.beUrl + href.Substring(1, href.Length - 1);
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            request.Headers.Add("method", "GET");
            request.Headers.Add("authority", "www.betexplorer.com");
            request.Headers.Add("scheme", "https");
            request.Headers.Add("path", "/match-odds/" + matchId + "/1/" + oddLoadMode);
            request.Headers.Add("x-requested-with", "XMLHttpRequest");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("accept-language", "en-US;q=0.8,ru-RU,ru;q=0.9,en;q=0.7");
            //request.Headers.Add("cookie", "_ga=GA1.2.11540757.1585916447; _hjid=b91dbff5-1411-47cc-bb61-195432b8ea14; my_timezone=%2B1; my_cookie_id=110484032; my_cookie_hash=3822e9f84dd7c28bc9f5bab57f88526f; _gid=GA1.2.1671225560.1588507164; js_cookie=1; _session_UA-191939-1=true; _hjIncludedInSample=1; page_cached=1; widget_pageViewCount=6");

            try
            {
                var response = await request.GetResponseAsync();

                var res = ReadStreamFromResponse(response);
                // {"odds":"
                // \"   =>  "
                // <\/   =>  </
                // "}
                //res = res.Replace(@"{""odds"":""", "");
                res = res.Replace("{\"odds\":\"", "");
                res = res.Replace("\"}", "");
                res = res.Replace(new string(new[] { '\\', '"' }), new string(new[] { '"' }));
                res = res.Replace(new string(new[] { '<', '\\', '/' }), new string(new[] { '<', '/' }));

                return res;
            }
            catch 
            {
                return String.Empty;
            }
        }

        private static async Task<string> GetOddHtml(match match, BeOddLoadMode oddLoadMode)
        {
            return await GetOddHtml(match.href, oddLoadMode);
        }

        private static List<odd> GetAhOdds(HtmlDocument doc, int parentId)
        {
            var result = new List<odd>();
            //<table class="table-main h-mb15 sortable" id="sortable-1">
            var tableNodes = doc.DocumentNode.SelectNodes(".//table[contains(@class, 'table-main h-mb15')]");

            foreach (var tableNode in tableNodes)
            {
                //<td class="table-main__doubleparameter">0.5</td>
                var foraStrings = tableNode.SelectNodes(".//td[@class='table-main__doubleparameter']")
                    .Select(x => x.InnerText).ToList();

                if (foraStrings.Any(x => x != foraStrings.First())) continue;

                var fora = foraStrings.First();

                if (!double.TryParse(fora, out _)) continue;

                //<td class="table-main__detail-odds" data-odd="1.07"></td>
                var oddsStrings = tableNode.SelectNodes(".//td[@class='table-main__detail-odds']")
                    .Select(x => x.GetAttributeValue("data-odd", "")).
                    Where(x => !string.IsNullOrEmpty(x)).
                    ToList();

                oddsStrings = oddsStrings.Skip(oddsStrings.Count - 2).ToList();
                if (oddsStrings.Count != 2) continue;
                var odds = oddsStrings.Select(double.Parse).ToList();

                result.Add(new odd() { oddType = "1(" + fora + ")", parentId = parentId, value = odds[0] });
                result.Add(new odd() { oddType = "2(" + fora + ")", parentId = parentId, value = odds[1] });
            }
            return result;
        }

        private static List<odd> Get1X2Odds(HtmlDocument doc, int parentId)
        {
            var result = new List<odd>();
            //<table class="table-main h-mb15 sortable" id="sortable-1">
            var tableNodes = doc.DocumentNode.SelectNodes(".//table[contains(@class, 'table-main h-mb15')]");
            var node = tableNodes.Last(); //А по сути, он там один, но похеру уж

            //<td class="table-main__detail-odds" data-odd="3.35"></td>

            var oddsStrings = node.SelectNodes(".//td[@class='table-main__detail-odds']")
                .Select(x => x.GetAttributeValue("data-odd", "")).
                Where(x => !string.IsNullOrEmpty(x)).
                ToList();

            oddsStrings = oddsStrings.Skip(oddsStrings.Count - 3).ToList();

            if (oddsStrings.Count != 3) return new List<odd>();

            result.Add(new odd() { oddType = "1", parentId = parentId, value = double.Parse(oddsStrings[0]) });
            result.Add(new odd() { oddType = "X ", parentId = parentId, value = double.Parse(oddsStrings[1]) });
            result.Add(new odd() { oddType = "2 ", parentId = parentId, value = double.Parse(oddsStrings[2]) });

            return result;
        }

        private static List<odd> GetTotalOdds(HtmlDocument doc, int parentId)
        {
            var result = new List<odd>();
            //<table class="table-main h-mb15 sortable" id="sortable-1">
            var tableNodes = doc.DocumentNode.SelectNodes(".//table[contains(@class, 'table-main h-mb15')]");

            foreach (var tableNode in tableNodes)
            {
                //<td class="table-main__doubleparameter">0.5</td>
                var totalStrings = tableNode.SelectNodes(".//td[@class='table-main__doubleparameter']")
                    .Select(x => x.InnerText).ToList();

                if (totalStrings.Any(x => x != totalStrings.First())) continue;

                if (!double.TryParse(totalStrings.First(), out var total)) continue;

                //<td class="table-main__detail-odds" data-odd="1.07"></td>
                var oddsStrings = tableNode.SelectNodes(".//td[@class='table-main__detail-odds']")
                    .Select(x => x.GetAttributeValue("data-odd", "")).
                    Where(x => !string.IsNullOrEmpty(x)).
                    ToList();

                oddsStrings = oddsStrings.Skip(oddsStrings.Count - 2).ToList();
                if (oddsStrings.Count != 2) continue;
                var odds = oddsStrings.Select(double.Parse).ToList();

                result.Add(new odd() { oddType = "Over " + total, parentId = parentId, value = odds[0] });
                result.Add(new odd() { oddType = "Under " + total, parentId = parentId, value = odds[1] });
            }
            return result;
        }

        private static List<odd> GetBtsOdds(HtmlDocument doc, int parentId)
        {
            var result = new List<odd>();
            //<table class="table-main h-mb15 sortable" id="sortable-1">
            var tableNodes = doc.DocumentNode.SelectNodes(".//table[contains(@class, 'table-main h-mb15')]");
            var node = tableNodes.Last(); //А по сути, он там один, но похеру уж

            //<td class="table-main__detail-odds" data-odd="3.35"></td>

            var oddsStrings = node.SelectNodes(".//td[@class='table-main__detail-odds']")
                .Select(x => x.GetAttributeValue("data-odd", "")).
                Where(x => !string.IsNullOrEmpty(x)).
                ToList();

            oddsStrings = oddsStrings.Skip(oddsStrings.Count - 2).ToList();

            if (oddsStrings.Count != 2) return new List<odd>();

            result.Add(new odd() { oddType = "BTS_YES", parentId = parentId, value = double.Parse(oddsStrings[0]) });
            result.Add(new odd() { oddType = "BTS_NO ", parentId = parentId, value = double.Parse(oddsStrings[1]) });

            return result;
        }
        #endregion
        
        #region Secondary functions
        public static void ParseMatchResult(string matchResult, out bool isCorrect, out int scored, out int missed)
        {
            isCorrect = false;
            scored = -1;
            missed = -1;

            //Contains any letter == notCorrect
            bool hasAnyLetter = matchResult.Any(Char.IsLetter);
            if (hasAnyLetter) return;

            //Parsing
            var pos = matchResult.IndexOf(BE_SCORE_DELIMITER);
            if (pos < 0) return;

            var strScore = matchResult.Substring(0, pos).Trim();
            var strMissed = matchResult.Substring(pos + 1).Trim();

            if (!Int32.TryParse(strScore, out scored)) return;
            if (!Int32.TryParse(strMissed, out missed)) return;

            isCorrect = true;
        }

        private static string ReadStreamFromResponse(WebResponse response)
        {
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader sr = new StreamReader(responseStream ?? throw new InvalidOperationException()))
            {
                return sr.ReadToEnd();
            }
        }

        public static string GetZipPath(league country, leagueUrl leagueUrl)
        {
            var zipFileName = country.name + "_urlId__" + leagueUrl.id + ".zip";
            return Settings.Default.zipFolder + zipFileName;
        }
        #endregion
    }

    public class BeMatch
    {
        public List<string> Names { get; }
        public string Href { get; }
        public string FinalScore { get; }
        public string Date { get; }
        public string Tag { get; }

        public bool IsCorrect { get; }

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
            if (!DateTime.TryParse(Date, out _)) return false;

            return true;
        }
    }

    public class MatchDetail
    {
        public bool AreResultsCorrect { get; set; }
        public int MatchId { get; private set; }

        public possibleResult FirstTimePossibleResult { get; set; }
        public possibleResult SecondTimePossibleResult { get; set; }

        public MatchDetail(string fTimeResult, string sTimeResult,int matchId)
        {
            MatchId = matchId;
            FirstTimePossibleResult = null;
            SecondTimePossibleResult = null;

            BetExplorerParser.ParseMatchResult(fTimeResult, out var isCorrect1, out var scored1, out var missed1);
            BetExplorerParser.ParseMatchResult(sTimeResult, out var isCorrect2, out var scored2, out var missed2);

            AreResultsCorrect = isCorrect1 && isCorrect2;

            if (AreResultsCorrect)
            {
                FirstTimePossibleResult = new possibleResult()
                {
                    isCorrect = isCorrect1,
                    value = fTimeResult,
                    scored = scored1,
                    missed = missed1,
                    total = scored1 + missed1,
                    diff = scored1 - missed1
                };

               SecondTimePossibleResult = new possibleResult()
                {
                    isCorrect = isCorrect2,
                    value = sTimeResult,
                    scored = scored2,
                    missed = missed2,
                    total = scored2 + missed2,
                    diff = scored2 - missed2
                };
            }
        }

        public override string ToString()
        {
            var list = new List<string>
            {
                AreResultsCorrect.ToString(),
                FirstTimePossibleResult.value,
                SecondTimePossibleResult.value
            };
            return string.Join("\n", list);
        }
    }

    public class StageUrl
    {
        //<a href="https://www.betexplorer.com/soccer/russia/premier-league-2013-2014/results/?stage=baGxDORM"
        #region Variables
        public string Href { get; }
        public string SelfTag { get; }
        public bool HasStage { get; }
        public string Stage { get; }
        public HtmlNode Node { get; }
        public string WebUrl { get; }
        public string ZipPath { get; }
        #endregion

        public string OuterTag { get; }

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
                var stagePos = Href.IndexOf("?stage=", StringComparison.Ordinal) + "?stage=".Length;
                Stage = Href.Substring(stagePos, Href.Length - stagePos);
            }

            WebUrl = Settings.Default.soccerUrl + country.url + leagueUrl.url + BetExplorerParser.RESULTS + Href;
        }
    }

    public sealed class BeOddLoadMode
    {
        private readonly string _name;

        public static readonly BeOddLoadMode _1X2 = new BeOddLoadMode("1x2");
        public static readonly BeOddLoadMode OU = new BeOddLoadMode("ou");
        public static readonly BeOddLoadMode AH = new BeOddLoadMode("ah");
        public static readonly BeOddLoadMode BTS = new BeOddLoadMode("bts");

        private BeOddLoadMode(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }

    }

/*    public sealed class OddType
    {
        private readonly string _name;

        public static readonly OddType _1 = new OddType("1");
        public static readonly OddType X = new OddType("X");
        public static readonly OddType _2 = new OddType("2");
        
        private OddType(string name)
        {
            this._name = name;
        }

        public override string ToString()
        {
            return _name;
        }

    }*/
}

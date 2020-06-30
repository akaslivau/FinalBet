using System;
using FinalBet.Properties;
using HtmlAgilityPack;

namespace FinalBet.Database
{
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
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace FinalBet.Database
{
    public class BeMatch
    {
        public List<string> Names { get; }
        public string Href { get; }
        public string FinalScore { get; }
        public string Date { get; }
        public string Tag { get; }
        public List<double> Odds { get; }

        public bool IsCorrect { get; }

        public BeMatch(List<string> names, string href, string finalScore, string date, string tag, List<double> odds)
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

            Odds = new List<double>();
            foreach (var odd in odds)
            {
                Odds.Add(odd);
            }
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
}
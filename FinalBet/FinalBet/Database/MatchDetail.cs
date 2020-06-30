using System.Collections.Generic;

namespace FinalBet.Database
{
    public class MatchDetail
    {
        public bool AreResultsCorrect { get; set; }
        public int MatchId { get; }

        public possibleResult FirstTimePossibleResult { get; set; }
        public possibleResult SecondTimePossibleResult { get; set; }

        public MatchDetail(string fTimeResult, string sTimeResult, int matchId)
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
}
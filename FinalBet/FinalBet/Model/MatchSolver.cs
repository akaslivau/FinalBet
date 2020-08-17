using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using FinalBet.Database;
using FinalBet.Properties;

namespace FinalBet.Model
{
    public static class MatchSolver
    {
        public static readonly Dictionary<Output, string> OutputStrings = new Dictionary<Output, string>
        {
            { Output.Win, "W" },
            { Output.Lose, "L" },
            { Output.Deuce, "D" },
            { Output.Empty, "na" },
            { Output.Null, "NaN" },
        };

        public static readonly Dictionary<Output, Brush> OutputBrushes = new Dictionary<Output, Brush>
        {
            { Output.Win, Brushes.Green },
            { Output.Lose, Brushes.Red },
            { Output.Deuce, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffdd00")) },
            { Output.Empty, Brushes.LightGray},
            { Output.Null, Brushes.Aqua },
        };


        //Если вдруг будет тормозить
        private static Dictionary<string, Output> _cachedOutputs = new Dictionary<string, Output>();

        public static Output Solve(IMatch match, SolveMode mode, bool useCache = false)
        {
            return mode.IsBookmakerMode ? SolveBook(match, mode) : SolveResult(match, mode, useCache);
        }

        private static Output SolveResult(IMatch match, SolveMode mode, bool useCache = false)
        {
            if (match.IsNull) return Output.Null;
            if (match.IsEmpty) return Output.Empty;
            
            var num = mode.SelectedMode.number;

            int total = match.Scored + match.Missed;
            int dif = match.Scored - match.Missed;

            //Total
            if (num == ModeOfSolveMode.Total)
                return total > mode.ModeParameter
                    ? Output.Win
                    : (total < mode.ModeParameter ? Output.Lose : Output.Deuce);

            //Fora
            if (num == ModeOfSolveMode.Fora)
            {
                var df = match.IsHome ? dif : -dif;
                var fSum = df + mode.ModeParameter;
                return fSum > 0 ? Output.Win : (fSum < 0 ? Output.Lose : Output.Deuce);
            }

            //Забито
            if (num == ModeOfSolveMode.Scored)
            {
                var goals = match.IsHome ? match.Scored : match.Missed;
                return goals > mode.ModeParameter
                    ? Output.Win
                    : (goals < mode.ModeParameter ? Output.Lose : Output.Deuce);
            }

            //Пропущено
            if (num == ModeOfSolveMode.Missed)
            {
                var goals = match.IsHome ? match.Missed : match.Scored;
                return goals > mode.ModeParameter
                    ? Output.Win
                    : (goals < mode.ModeParameter ? Output.Lose : Output.Deuce);
            }

            //Обе забьют
            if (num == ModeOfSolveMode.BTS)
            {
                return (match.Scored > 0.5 && match.Missed > 0.5) ? Output.Win : Output.Lose;
            }
           
            //Чет-нечет
            if (num == ModeOfSolveMode.CN)
            {
                return total % 2 == 0 ? Output.Win : Output.Lose;
            }
            return Output.Empty;
        }

        private static Output SolveBook(IMatch match, SolveMode mode)
        {
            //TODO: доделать
            if (match.IsNull) return Output.Null;
            if (match.IsEmpty) return Output.Empty;

            if(match.Odds == null || !match.Odds.Any()) return Output.Null;
            
            if( Math.Abs(match.Odds.ElementAt(0).Value - match.Odds.ElementAt(1).Value) < Settings.Default.oddMinDif)
            {
                return Output.Null;
            }
            
            var min = match.Odds.Min(x => x.Value);
            var minKey = OddType.GetClearType(match.Odds.Single(x => Math.Abs(x.Value - min) < 0.0005).Key);

            var res = SolveResult(match, mode);
            var output = OutputKeys[minKey];
            
            return res == output ? Output.Win : Output.Lose;
        }

        private static Dictionary<string, Output> OutputKeys = new Dictionary<string, Output>()
        {
            {OddType.Over, Output.Win},
            {OddType.Under, Output.Lose},
        };

    }

    public sealed class ModeOfSolveMode
    {
        private readonly int _number;

        public static readonly ModeOfSolveMode Total = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Тотал"));
        public static readonly ModeOfSolveMode Fora = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Фора"));
        public static readonly ModeOfSolveMode BTS = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Обе забьют"));

        public static readonly ModeOfSolveMode Scored = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Забито"));
        public static readonly ModeOfSolveMode Missed = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Пропущено"));
        public static readonly ModeOfSolveMode CN = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Чет/нечет"));

        private ModeOfSolveMode(solveMode mode)
        {
            this._number = mode.number;
        }

        public override string ToString()
        {
            return _number.ToString();
        }

        public static implicit operator int(ModeOfSolveMode o) => o._number;

        public static implicit operator solveMode(ModeOfSolveMode m) => Global.PossibleModes.Single(x => x.number == m._number);
        public static explicit operator ModeOfSolveMode(solveMode b) => new ModeOfSolveMode(b);



    }

    public enum Output
    {
        Win = 1,
        Lose = 0,
        Deuce = -1,
        Empty = -2,
        Null = -3
    }

    public interface IMatch
    {
        bool IsEmpty { get; set; }
        bool IsNull { get; set; }

        bool IsHome { get; set; }

        int Scored { get; set; }
        int Missed { get; set; }

        Dictionary<string, double> Odds { get; set; }
    }
}

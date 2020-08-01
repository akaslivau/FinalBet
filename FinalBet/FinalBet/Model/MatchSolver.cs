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
            { Output.Na, "na" },
            { Output.Nan, "NaN" },
        };

        public static readonly Dictionary<Output, Brush> OutputBrushes = new Dictionary<Output, Brush>
        {
            { Output.Win, Brushes.Green },
            { Output.Lose, Brushes.Red },
            { Output.Deuce, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffdd00")) },
            { Output.Na, Brushes.LightGray },
            { Output.Nan, Brushes.Aqua },
        };


        //Если вдруг будет тормозить
        private static Dictionary<string, Output> _cachedOutputs = new Dictionary<string, Output>();

        public static Output Solve(IMatch match, SolveMode mode, bool useCache = false)
        {
            return mode.IsBookmakerMode ? SolveBook(match, mode) : SolveResult(match, mode, useCache);
        }

        private static Output SolveResult(IMatch match, SolveMode mode, bool useCache = false)
        {
            if (match.IsNaN) return Output.Nan;
            if (match.IsNa) return Output.Na;
            
            var num = mode.SelectedMode.number;

            //Total
            if (num == ModeOfSolveMode.Total)
                return match.Total > mode.ModeParameter
                    ? Output.Win
                    : (match.Total < mode.ModeParameter ? Output.Lose : Output.Deuce);

            //Fora
            if (num == ModeOfSolveMode.Fora)
            {
                var df = match.IsHome ? match.Dif : -match.Dif;
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
                return match.Total % 2 == 0 ? Output.Win : Output.Lose;
            }
            return Output.Na;
        }

        private static Output SolveBook(IMatch match, SolveMode mode)
        {
            //TODO: доделать
            if (match.IsNaN) return Output.Nan;
            if (match.IsNa) return Output.Na;

            if(match.Odds == null || !match.Odds.Any()) return Output.Nan;
            
            if( Math.Abs(match.Odds.ElementAt(0).Value - match.Odds.ElementAt(1).Value) < Settings.Default.oddMinDif)
            {
                return Output.Nan;
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
        Na = -2,
        Nan = -3
    }

    public interface IMatch
    {
        bool IsNa { get; set; }
        bool IsNaN { get; set; }

        bool IsHome { get; set; }

        int Scored { get; set; }
        int Missed { get; set; }

        int Total { get; set; }
        int Dif { get; set; }

        Dictionary<string, double> Odds { get; set; }
    }
}

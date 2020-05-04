using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

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
            if (match.IsNaN) return Output.Nan;
            if (match.IsNa) return Output.Na;
            
            //Тут жесткая привязка к режимам, че поделаешь
            var num = mode.SelectedMode.number;

            //Total
            if (num == 0)
                return match.Total > mode.ModeParameter
                    ? Output.Win
                    : (match.Total < mode.ModeParameter ? Output.Lose : Output.Deuce);

            //Fora
            if (num == 1)
            {
                var df = match.IsHome ? match.Dif : -match.Dif;
                var fSum = df + mode.ModeParameter;
                return fSum > 0 ? Output.Win : (fSum < 0 ? Output.Lose : Output.Deuce);
            }

            //Голы хозяев
            if (num == 2)
            {
                var goals = match.IsHome ? match.Scored : match.Missed;
                return goals > mode.ModeParameter
                    ? Output.Win
                    : (goals < mode.ModeParameter ? Output.Lose : Output.Deuce);
            }

            //Голы гостей
            if (num == 3)
            {
                var goals = match.IsHome ? match.Missed : match.Scored;
                return goals > mode.ModeParameter
                    ? Output.Win
                    : (goals < mode.ModeParameter ? Output.Lose : Output.Deuce);
            }

            //Обе забьют
            if (num == 4)
            {
                return (match.Scored > 0.5 && match.Missed > 0.5) ? Output.Win : Output.Lose;
            }
           
            //Чет-нечет
            if (num == 5)
            {
                return match.Total % 2 == 0 ? Output.Win : Output.Lose;
            }
            return Output.Na;
        }
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
    }
    


    public class RGmatch:IMatch
    {
        public bool IsNa { get; set; }
        public bool IsNaN { get; set; }

        public bool IsHome { get; set; }
        public int Scored { get; set; }
        public int Missed { get; set; }
        public int Total { get; set; }
        public int Dif { get; set; }

        public RGmatch(bool isHome, int scored, int missed, int total, int dif)
        {
            IsNa = false;
            IsHome = isHome;
            Scored = scored;
            Missed = missed;
            Total = total;
            Dif = dif;
        }

        public static RGmatch GetEmpty()
        {
            var res = new RGmatch(true, -1, -1, -1, -1) {IsNa = true};
            return res;
        }

        public static RGmatch GetNanMatch()
        {
            var res = new RGmatch(true, -1, -1, -1, -1) {IsNaN = true, IsNa = false};
            return res;
        }
    }
}

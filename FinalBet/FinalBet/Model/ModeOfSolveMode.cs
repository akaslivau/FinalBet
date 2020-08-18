using System.Linq;

namespace FinalBet.Model
{
    public sealed class ModeOfSolveMode
    {
        private readonly int _number;

        public static readonly ModeOfSolveMode Total = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Тотал"));
        public static readonly ModeOfSolveMode Fora = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Фора"));
        public static readonly ModeOfSolveMode BTS = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Обе забьют"));

        public static readonly ModeOfSolveMode Scored = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Забито"));
        public static readonly ModeOfSolveMode Missed = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Пропущено"));
        public static readonly ModeOfSolveMode CN = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "Чет/нечет"));
        public static readonly ModeOfSolveMode X_12 = new ModeOfSolveMode(Global.PossibleModes.Single(x => x.name == "X/12"));

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
}
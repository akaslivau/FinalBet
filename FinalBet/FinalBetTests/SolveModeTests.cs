using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;
using FinalBet.Model;
using FinalBet.Properties;
using NUnit.Framework;

namespace FinalBetTests
{
    [TestFixture]
    public class SolveModeTests
    {
        private List<RGmatch> _matches = null;
        public List<RGmatch> Matches
        {
            get
            {
                if (_matches == null)
                {
                    _matches = new List<RGmatch>();

                    var size = 20;
                    for (int i = 0; i < size; i++)
                    for (int j = 0; j < size; j++)
                    {
                        _matches.Add(new RGmatch(true, i, j, null));
                    }
                }

                return _matches;
            }
        }

        [Test]
        public void TotalTests()
        {
            for (int i = 0; i < 10; i++)
            {
                var total = 0.5 + i;
                var solveMode = new SolveMode {
                    ModeParameter = total,
                    IsBookmakerMode = false,
                    MatchPeriod = 0,
                    SelectedMode  = ModeOfSolveMode.Total
                };

                var more = Matches.Where(x => (x.Scored + x.Missed) > total).ToList();
                var less = Matches.Where(x => (x.Scored + x.Missed) < total).ToList();

                Assert.AreEqual(more.All(z => MatchSolver.Solve(z, solveMode) == Output.Win), true);
                Assert.AreEqual(less.All(z => MatchSolver.Solve(z, solveMode) == Output.Lose), true);
            }
        }

        [Test]
        public void ForaTests()
        {
            for (int i = 0; i < 20; i++)
            {
                var fora = -5 + i * 0.5;
                var solveMode = new SolveMode
                {
                    ModeParameter = fora,
                    IsBookmakerMode = false,
                    MatchPeriod = 0,
                    SelectedMode = ModeOfSolveMode.Fora
                };

                var win = Matches.Where(x => (x.Scored + fora - x.Missed) > 0).ToList();
                var deuce = Matches.Where(x => (x.Scored + fora - x.Missed) == 0).ToList();
                var lose = Matches.Where(x => (x.Scored + fora - x.Missed) < 0).ToList();

                Assert.AreEqual(win.All(z => MatchSolver.Solve(z, solveMode) == Output.Win), true);
                Assert.AreEqual(deuce.All(z => MatchSolver.Solve(z, solveMode) == Output.Deuce), true);
                Assert.AreEqual(lose.All(z => MatchSolver.Solve(z, solveMode) == Output.Lose), true);

                var guestMatches = Matches.Select(x => new RGmatch(false, x.Scored, x.Missed, null)).ToList();

                var gwin = guestMatches.Where(x => (x.Missed + fora - x.Scored) > 0).ToList();
                var gdeuce = guestMatches.Where(x => (x.Missed + fora - x.Scored) == 0).ToList();
                var glose = guestMatches.Where(x => (x.Missed + fora - x.Scored) < 0).ToList();

                Assert.AreEqual(gwin.All(z => MatchSolver.Solve(z, solveMode) == Output.Win), true);
                Assert.AreEqual(gdeuce.All(z => MatchSolver.Solve(z, solveMode) == Output.Deuce), true);
                Assert.AreEqual(glose.All(z => MatchSolver.Solve(z, solveMode) == Output.Lose), true);
            }
        }

        [Test]
        public void NullTests()
        {
            var rnd = new Random();
            for (int i = 0; i < 100; i++)
            {
                var solveMode = new SolveMode
                {
                    ModeParameter = 2.5,
                    IsBookmakerMode = false,
                    MatchPeriod = 0,
                    SelectedMode = ModeOfSolveMode.Total
                };

                var m = new RGmatch(true, rnd.Next(-5, 3), rnd.Next(-5, 3), null);
                if (m.IsNull)
                {
                    Assert.AreEqual(m.Missed == -1 && m.Scored == -1, true);
                    Assert.AreEqual(MatchSolver.Solve(m, solveMode), Output.Null);
                }
                else
                {
                    Assert.AreEqual(m.Missed >=0 && m.Scored >=0, true);
                }
            }
        }
    }
}

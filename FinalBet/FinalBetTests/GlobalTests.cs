using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Model;
using NUnit.Framework;

namespace FinalBetTests
{
    [TestFixture]
    public class GlobalTests
    {
        [Test]
        public void GlobalVariablesLoaded()
        {
            Assert.AreEqual(Global.PossibleModes.Any(), true);
            Assert.AreEqual(Global.LeagueMarks.Any(), true);
            Assert.AreEqual(Global.MatchLoadModes.Any(), true);
        }
    }
}

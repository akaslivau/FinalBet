using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalBet.Database;
using NUnit.Framework;

namespace FinalBetTests
{
    [SetUpFixture]
    public class Config
    {
        public static string EnvironmentVariable = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

        public static string GetFile(string partialPath)
        {
            return Path.Combine(EnvironmentVariable, partialPath);
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            var path = GetFile("connection");
            var conString = File.ReadAllText(path, Encoding.GetEncoding("windows-1251"));
            Connection.Initialize(conString);
        }
    }
}

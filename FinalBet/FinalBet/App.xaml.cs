using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Database;
using FinalBet.Model;
using FinalBet.Properties;
using FinalBet.ViewModel;
using Serilog;
using Serilog.Core;

namespace FinalBet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var log = new LoggerConfiguration()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Month)
                .CreateLogger();

            Log.Logger = log;
           
            Log.Information("Application started");
            Global.Current.Infos++;

            var conString = File.Exists("connection")
                ? File.ReadAllText("connection", Encoding.GetEncoding("windows-1251"))
                : Settings.Default.soccerConnectionString;

            Connection.Initialize(conString);
            if (!Connection.IsSuccessful)
            {
                Log.Fatal("Соединение с БД не установлено");
                Global.Current.Errors++;
            }

            var culture = new CultureInfo("en-Us")
            {
                DateTimeFormat = { ShortDatePattern = "dd.MM.yyyy", LongTimePattern = "" }
            };
            
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            base.OnStartup(e);
            
            var window = new MainWindow();
           
            // Create the ViewModel to which 
            // the main window binds.
            var viewModel = new MainWindowViewModel();

            // When the ViewModel asks to be closed, 
            // close the window.
            EventHandler handler = null;
            handler = delegate
            {
                viewModel.RequestClose -= handler;
                Log.CloseAndFlush();
                window.Close();
            };
            viewModel.RequestClose += handler;

            // Allow all controls in the window to 
            // bind to the ViewModel by setting the 
            // DataContext, which propagates down 
            // the element tree.
            window.DataContext = viewModel;
            window.Show();
        }
    }
}

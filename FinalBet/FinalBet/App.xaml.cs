﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Database;
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
            Connection.Initialize(Settings.Default.soccerConnectionString);

            var culture = new CultureInfo("en-Us")
            {
                DateTimeFormat = { ShortDatePattern = "dd.MM.yyyy", LongTimePattern = "" }
            };

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

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

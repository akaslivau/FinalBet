﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Database;
using FinalBet.Framework;

namespace FinalBet.Model
{
    public class Global: DependencyObject
    {
        static Global()
        {
            Current = new Global();
        }

        public static Global Current { get; private set; }

        private static ObservableCollection<leagueMark> _leagueMarks = null;
        public static ObservableCollection<leagueMark> LeagueMarks
        {
            get
            {
                if (_leagueMarks != null) return _leagueMarks;

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var table = cntx.GetTable<leagueMark>().ToList();
                    _leagueMarks = new ObservableCollection<leagueMark>(table);
                }

                return _leagueMarks;
            }
        }


        #region DependencyProperties

        public static readonly DependencyProperty InfosProperty = 
            DependencyProperty.Register("Infos", 
                typeof(int), 
                typeof(Global), 
                new UIPropertyMetadata(default(int)));

        public static readonly DependencyProperty WarningsProperty =
            DependencyProperty.Register("Warnings",
                typeof(int),
                typeof(Global),
                new UIPropertyMetadata(default(int)));

        public static readonly DependencyProperty ErrorsProperty =
            DependencyProperty.Register("Errors",
                typeof(int),
                typeof(Global),
                new UIPropertyMetadata(default(int)));

        public int Infos
        {
            get => (int) GetValue(InfosProperty);
            set => SetValue(InfosProperty, value);
        }

        public int Warnings
        {
            get => (int) GetValue(WarningsProperty);
            set => SetValue(WarningsProperty, value);
        }

        public int Errors
        {
            get => (int) GetValue(ErrorsProperty);
            set => SetValue(ErrorsProperty, value);
        }

        #endregion


    }
}
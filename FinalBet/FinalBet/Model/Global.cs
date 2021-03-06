﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FinalBet.Database;
using FinalBet.Framework;
using FinalBet.Model.Filtering;

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
                if(!Connection.IsSuccessful) return new ObservableCollection<leagueMark>();

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var table = cntx.GetTable<leagueMark>().ToList();
                    _leagueMarks = new ObservableCollection<leagueMark>(table);
                }

                return _leagueMarks;
            }
        }

        private static ObservableCollection<solveMode> _possibleModes = null;
        public static ObservableCollection<solveMode> PossibleModes
        {
            get
            {
                if (_possibleModes != null) return _possibleModes;
                if (!Connection.IsSuccessful) return new ObservableCollection<solveMode>();

                using (var cntx = new SqlDataContext(Connection.ConnectionString))
                {
                    var table = cntx.GetTable<solveMode>().ToList();
                    _possibleModes = new ObservableCollection<solveMode>(table);
                }
                return _possibleModes;
            }
        }

        private static ObservableCollection<MatchMethod> _filterMethods = null;
        public static ObservableCollection<MatchMethod> FilterMethods
        {
            get
            {
                if (_filterMethods == null)
                {
                    _filterMethods = new ObservableCollection<MatchMethod>();
                    var methods = typeof(StaticMatchMethods).GetMethods(
                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var methodInfo in methods)
                    {
                        var attr = methodInfo.GetCustomAttributes(typeof(FilterAttribute), false);
                        if (attr.Length > 0)
                        {
                            var at = (FilterAttribute) attr[0];
                            _filterMethods.Add(new MatchMethod(at.Id, methodInfo.Name, at.Description, at.IsHistorical, at.NeedSolveMode));
                        }
                    }
                }
                return _filterMethods;
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

        public static ObservableCollection<string> MatchLoadModes = new ObservableCollection<string>()
        {
            "Выбранная ссылка",
            "Турнир для страны",
            "Турнир для всех стран",
            "Все турниры"
        };
    }
}

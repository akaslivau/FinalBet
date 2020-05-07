﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FinalBet.Model
{
    public class SolveMode: INotifyPropertyChanged
    {
        #region Properties
        private bool? isHome;
        public bool? IsHome
        {
            get
            {
                return isHome;
            }
            set
            {
                if (isHome == value) return;
                isHome = value;
                OnPropertyChanged("IsHome");
                OnPropertyChanged("IsHomeString");
            }
        }

        public string IsHomeString => IsHome.HasValue ? ((bool) IsHome ? "Домашние матчи" : "Гостевые матчи") : "Все матчи";

        private int _matchPeriod;
        public int MatchPeriod
        {
            get => _matchPeriod;
            set
            {
                if (_matchPeriod == value) return;
                _matchPeriod = value;
                OnPropertyChanged("MatchPeriod");
                OnPropertyChanged("MatchPeriodString");
            }
        }

        public string MatchPeriodString => _matchPeriod == 0 ? "Итоговый счет" : (_matchPeriod == 1 ? "1-й тайм" : "2-й тайм");

        private solveMode _selectedMode;
        public solveMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode == value) return;
                _selectedMode = value;
                OnPropertyChanged("SelectedMode");
                IsParameterEnabled = value.hasParameter;
            }
        }

        private double _modeParameter = 2.5D;
        public double ModeParameter
        {
            get => _modeParameter;
            set
            {
                if (_modeParameter == value) return;
                _modeParameter = value;
                OnPropertyChanged("ModeParameter");
            }
        }

        private bool _isParameterEnabled = true;
        public bool IsParameterEnabled
        {
            get
            {
                return _isParameterEnabled;
            }
            set
            {
                if (_isParameterEnabled == value) return;
                _isParameterEnabled = value;
                OnPropertyChanged("IsParameterEnabled");
            }
        }

        private bool _isBookmakerMode;
        public bool IsBookmakerMode
        {
            get => _isBookmakerMode;
            set
            {
                if (_isBookmakerMode == value) return;
                _isBookmakerMode = value;
                OnPropertyChanged("IsBookmakerMode");
                OnPropertyChanged("IsBookmakerModeString");
            }
        }

        public string IsBookmakerModeString => IsBookmakerMode ? "Режим угадывания букмекером" : "Классический режим";
        #endregion

        public SolveMode()
        {
            if (Global.PossibleModes.Any()) _selectedMode = Global.PossibleModes.First();
        }

        #region Debugging Aides

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This 
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,  
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (this.ThrowOnInvalidPropertyName)
                    throw new Exception(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might 
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

        #endregion // Debugging Aides

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            this.VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion // INotifyPropertyChanged Members
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SharpVectors.Converters;

namespace FinalBet.Extensions
{
    public class SvgViewboxAttachedProperties : DependencyObject
    {
        public static string GetSource(DependencyObject obj)
        {
            return (string)obj.GetValue(SourceProperty);
        }

        public static void SetSource(DependencyObject obj, string value)
        {
            obj.SetValue(SourceProperty, value);
        }

        private static void OnSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var svgControl = obj as SvgViewbox;
            if (svgControl != null)
            {
                var path = (string)e.NewValue;
                svgControl.Source = string.IsNullOrWhiteSpace(path) ? default(Uri) : new Uri(path, UriKind.RelativeOrAbsolute);
            }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached("Source",
                typeof(string), typeof(SvgViewboxAttachedProperties),
                // default value: null
                new PropertyMetadata(null, OnSourceChanged));
    }
}

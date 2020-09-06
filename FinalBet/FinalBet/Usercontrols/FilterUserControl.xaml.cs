using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FinalBet.Model;
using FinalBet.Model.Filtering;

namespace FinalBet.Usercontrols
{
    /// <summary>
    /// Interaction logic for FilterUserControl.xaml
    /// </summary>
    public partial class FilterUserControl : UserControl
    {
        public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
            "Filter", typeof(MatchPropertyRepoViewModel), typeof(FilterUserControl), new PropertyMetadata(default(MatchPropertyRepoViewModel)));

        public MatchPropertyRepoViewModel Filter
        {
            get { return (MatchPropertyRepoViewModel) GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }

        public FilterUserControl()
        {
            InitializeComponent();
        }
    }
}

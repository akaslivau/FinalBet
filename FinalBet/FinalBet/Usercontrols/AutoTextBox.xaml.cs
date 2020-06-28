using System;
using System.Collections;
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

namespace FinalBet.Usercontrols
{
    /// <summary>
    /// Interaction logic for AutoTextBox.xaml
    /// </summary>
    public partial class AutoTextBox : UserControl
    {
        public static readonly DependencyProperty FilterSourceProperty = DependencyProperty.Register(
            "FilterSource", typeof(IList), typeof(AutoTextBox), new PropertyMetadata(default(IList)));

        public IList FilterSource
        {
            get { return (IList) GetValue(FilterSourceProperty); }
            set { SetValue(FilterSourceProperty, value); }
        }

        public static readonly DependencyProperty SelectedCommandProperty = DependencyProperty.Register(
            "SelectedCommand", typeof(ICommand), typeof(AutoTextBox), new PropertyMetadata(default(ICommand)));

        public ICommand SelectedCommand
        {
            get { return (ICommand) GetValue(SelectedCommandProperty); }
            set { SetValue(SelectedCommandProperty, value); }
        }

        public AutoTextBox()
        {
            InitializeComponent();
        }

        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            bool found = false;
            string query = (sender as TextBox).Text;
            if(query.Length < 3) return;
            

            if (query.Length == 0)
            {
                // Clear
                ResultStack.Children.Clear();
                SuggestPopup.IsOpen = false;
            }
            else
            {
                SuggestPopup.IsOpen = true;
            }

            // Clear the list
            ResultStack.Children.Clear();

            // Add the result
            foreach (var obj in FilterSource)
            {
                if (obj.ToString().ToLower().Contains(query.ToLower()))
                {
                    // The word starts with this... Autocomplete must work
                    addItem(obj.ToString());
                    found = true;
                }
            }

            if (ResultStack.Children.Count == 1)
            {
                SuggestPopup.IsOpen = false;
                TextBox.Text = (ResultStack.Children[0] as TextBlock).Text;
            }


            if (!found)
            {
                ResultStack.Children.Add(new TextBlock() { Text = "No results found." });
            }
        }

        private void addItem(string text)
        {
            TextBlock block = new TextBlock();

            // Add the text
            block.Text = text;

            // A little style...
            block.Margin = new Thickness(2, 3, 2, 3);
            block.Cursor = Cursors.Hand;

            // Mouse events
            block.MouseLeftButtonUp += (sender, e) =>
            {
                TextBox.Text = (sender as TextBlock).Text;
                SuggestPopup.IsOpen = false;
                SelectedCommand.Execute(null);
            };

            block.MouseEnter += (sender, e) =>
            {
                TextBlock b = sender as TextBlock;
                b.Background = Brushes.PeachPuff;
            };

            block.MouseLeave += (sender, e) =>
            {
                TextBlock b = sender as TextBlock;
                b.Background = Brushes.Transparent;
            };

            // Add to the panel
            ResultStack.Children.Add(block);
        }
    }
}

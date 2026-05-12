using System.Windows;
using System.Windows.Input;

namespace Klyr.Views
{
    public partial class LegalWindow : Window
    {
        public LegalWindow()
        {
            InitializeComponent();
        }
        private void Close_Click(object sender, RoutedEventArgs e)   => Close();
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}

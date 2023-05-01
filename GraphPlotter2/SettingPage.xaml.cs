using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace GraphPlotter2
{
    /// <summary>
    /// SettingPage.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
        }

        private void textBoxPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 0-9のみ
            e.Handled = !new Regex("[0-9]").IsMatch(e.Text);
        }
        private void textBoxPrice_PreviewTextInput_withDecimal(object sender, TextCompositionEventArgs e)
        {
            // 0-9と.のみ
            e.Handled = !new Regex("[0-9.]").IsMatch(e.Text);
        }
        private void textBoxPrice_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // 貼り付けを許可しない
            if (e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            var mainPage = new MainPage();
            NavigationService.Navigate(mainPage);
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("本当に保存しなくてよいですか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var mainPage = new MainPage();
                NavigationService.Navigate(mainPage);
            }
        }
    }
}

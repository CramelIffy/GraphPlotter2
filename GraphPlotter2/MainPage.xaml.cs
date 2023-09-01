using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GraphPlotter2
{
    /// <summary>
    /// MainPage.xaml の相互作用ロジック
    /// </summary>
    public partial class MainPage : Page
    {
        private OpenFileDialog ofd;

        DataModifier.DataModifier thrustDatas;
        public MainPage()
        {
            InitializeComponent();

            ofd = new OpenFileDialog();
            ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            ofd.Title = "開くファイルを選択してください";

            thrustDatas = new DataModifier.DataModifier();
        }

        private void OpenCsv(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                thrustDatas.SetData(ofd.FileName, false);
            }
        }
        private void OpenBin(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                thrustDatas.SetData(ofd.FileName, true);
            }
        }

        private void SaveImage(object sender, RoutedEventArgs e)
        {
        }

        private void OpenSetting(object sender, RoutedEventArgs e)
        {
            var settingPage = new SettingPage();
            NavigationService.Navigate(settingPage);
        }
        private void OpenAbout(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("GraphPlotter V2.0.0\nMade by Fujie Riu\nTokushimaUniv. Rocket Project", "バージョン情報", MessageBoxButton.OK);
        }
        private void Exit(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("本当に終了しますか？", "終了", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) Application.Current.Shutdown();
        }
    }
}

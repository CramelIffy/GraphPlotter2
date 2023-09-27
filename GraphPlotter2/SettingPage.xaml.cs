using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace GraphPlotter2
{
    /// <summary>
    /// SettingPage.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingPage : Page
    {
        private void CloseSettingPage()
        {
            var mainPage = new MainPage();
            NavigationService.Navigate(mainPage);
        }

        private void SetConfigToUi()
        {
            MainGraph.IsChecked = MainWindow.SettingIO.Data.MainGraph;
            SubGraph.IsChecked = MainWindow.SettingIO.Data.SubGraph;
            BurningTime.IsChecked = MainWindow.SettingIO.Data.BurningTime;
            MaxThrust.IsChecked = MainWindow.SettingIO.Data.MaxThrust;
            AverageThrust.IsChecked = MainWindow.SettingIO.Data.AverageThrust;
            TotalImpulse.IsChecked = MainWindow.SettingIO.Data.TotalImpulse;
            MainGraphName.Text = MainWindow.SettingIO.Data.MainGraphName;
            SubGraphName.Text = MainWindow.SettingIO.Data.SubGraphName;
            SubGraphOpacity.Text = MainWindow.SettingIO.Data.SubGraphOpacity.ToString();
            UndenoisedGraphOpacity.Text = MainWindow.SettingIO.Data.UndenoisedGraphOpacity.ToString();
            BurningTimeOpacity.Text = MainWindow.SettingIO.Data.BurningTimeOpacity.ToString();
            IgnitionDetectionThreshold.Text = MainWindow.SettingIO.Data.IgnitionDetectionThreshold.ToString();
            BurnoutDetectionThreshold.Text = MainWindow.SettingIO.Data.BurnoutDetectionThreshold.ToString();
            PrefixOfTimeCSV.Text = MainWindow.SettingIO.Data.PrefixOfTimeCSV.ToString("F6");
            PrefixOfTimeBIN.Text = MainWindow.SettingIO.Data.PrefixOfTimeBIN.ToString("F6");
            SlopeCSV.Text = MainWindow.SettingIO.Data.SlopeCSV.ToString();
            InterceptCSV.Text = MainWindow.SettingIO.Data.InterceptCSV.ToString();
            SlopeBIN.Text = MainWindow.SettingIO.Data.SlopeBIN.ToString();
            InterceptBIN.Text = MainWindow.SettingIO.Data.InterceptBIN.ToString();
        }

        private void GetConfigFromUi()
        {
            MainWindow.SettingIO.Data.MainGraph = MainGraph.IsChecked ?? true;
            MainWindow.SettingIO.Data.SubGraph = SubGraph.IsChecked ?? true;
            MainWindow.SettingIO.Data.BurningTime = BurningTime.IsChecked ?? true;
            MainWindow.SettingIO.Data.MaxThrust = MaxThrust.IsChecked ?? true;
            MainWindow.SettingIO.Data.AverageThrust = AverageThrust.IsChecked ?? true;
            MainWindow.SettingIO.Data.TotalImpulse = TotalImpulse.IsChecked ?? true;
            MainWindow.SettingIO.Data.MainGraphName = MainGraphName.Text;
            MainWindow.SettingIO.Data.SubGraphName = SubGraphName.Text;
            MainWindow.SettingIO.Data.SubGraphOpacity = Convert.ToInt32(SubGraphOpacity.Text);
            MainWindow.SettingIO.Data.UndenoisedGraphOpacity = Convert.ToInt32(UndenoisedGraphOpacity.Text);
            MainWindow.SettingIO.Data.BurningTimeOpacity = Convert.ToInt32(BurningTimeOpacity.Text);
            MainWindow.SettingIO.Data.IgnitionDetectionThreshold = Convert.ToInt32(IgnitionDetectionThreshold.Text);
            MainWindow.SettingIO.Data.BurnoutDetectionThreshold = Convert.ToInt32(BurnoutDetectionThreshold.Text);
            MainWindow.SettingIO.Data.PrefixOfTimeCSV = Convert.ToDouble(PrefixOfTimeCSV.Text);
            MainWindow.SettingIO.Data.PrefixOfTimeBIN = Convert.ToDouble(PrefixOfTimeBIN.Text);
            MainWindow.SettingIO.Data.SlopeCSV = Convert.ToDouble(SlopeCSV.Text);
            MainWindow.SettingIO.Data.InterceptCSV = Convert.ToDouble(InterceptCSV.Text);
            MainWindow.SettingIO.Data.SlopeBIN = Convert.ToDouble(SlopeBIN.Text);
            MainWindow.SettingIO.Data.InterceptBIN = Convert.ToDouble(InterceptBIN.Text);
        }

        public SettingPage()
        {
            InitializeComponent();
            try
            {
                if (MainWindow.SettingIO.IsConfigFileExist())
                    MainWindow.SettingIO.LoadConfig();
                SetConfigToUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show("設定ファイルが読み込めません。\n" + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseSettingPage();
            }
        }

        private void textBoxPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 0-9のみ
            e.Handled = !new Regex("[0-9-]").IsMatch(e.Text);
        }
        private void textBoxPrice_PreviewTextInput_withDecimal(object sender, TextCompositionEventArgs e)
        {
            // 0-9と.のみ
            e.Handled = !new Regex("[0-9.-]").IsMatch(e.Text);
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
            try
            {
                GetConfigFromUi();
                MainWindow.SettingIO.WriteConfig();
            }
            catch (Exception ex)
            {
                if (MessageBox.Show("設定ファイルが保存できませんでした。\n" + ex.Message + "\n保存せず設定を終了しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    CloseSettingPage();
            }
            CloseSettingPage();
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("本当に保存しなくてよいですか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                CloseSettingPage();
        }

        private void InitConfig(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("本当に設定を初期化しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                MainWindow.SettingIO.InitConfig();
                SetConfigToUi();
            }
        }
    }
}

using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
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
        private readonly OpenFileDialog ofd;

        private readonly SaveFileDialog sfd;

        private ScottPlot.Plot mainPlot;

        readonly DataModifier.DataModifier thrustDatas;
        public MainPage()
        {
            InitializeComponent();

            ofd = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Title = "開くファイルを選択してください"
            };

            sfd = new SaveFileDialog
            {
                Filter = "CSVファイル (*.csv)|*.csv",
                Title = "保存場所と名前を決定してください",
                FileName = "output.csv"
            };

            thrustDatas = new DataModifier.DataModifier();

            mainPlot = new ScottPlot.Plot();
            mainPlot.Clear();
            mainPlot.Title("TEST");
            mainPlot.Render();
        }

        private void PlotData()
        {
            
        }

        private void OpenCsv(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    thrustDatas.SetData(ofd.FileName, false);
                    PlotData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("読み込みに失敗しました。\nErrorMessage: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OpenBin(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    thrustDatas.SetData(ofd.FileName, true);
                    PlotData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("読み込みに失敗しました。\nErrorMessage: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveImage(object sender, RoutedEventArgs e)
        {
        }

        private async void SaveDataAsCsv(object sender, RoutedEventArgs e)
        {
            try
            {
                thrustDatas.GetData(true);
            }catch (Exception)
            {
                MessageBox.Show("データが読み込まれていません", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (sfd.ShowDialog() == true)
            {
                string filePath = sfd.FileName;

                using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                using (StreamWriter writer = new(fileStream, Encoding.UTF8, 4096))
                {
                    await writer.WriteLineAsync("time(s),thrust(N),denoisedThrust(N),burningTime(s),maxThrust(N),averageThrust(N),totalImpluse(N·s)");

                    bool noDataWrittenYet = true;
                    StringBuilder buffer = new();
                    for (int i = thrustDatas.GetData(true).ignitionIndex; i <= thrustDatas.GetData(true).burnoutIndex; i++)
                    {
                        buffer.Clear();
                        buffer.Append(thrustDatas.GetData(true).time[i].ToString("F7"));
                        buffer.Append("," + thrustDatas.GetData(true).thrust[i].ToString("F7"));
                        buffer.Append("," + thrustDatas.GetData(true).denoisedThrust[i].ToString("F7"));
                        if(noDataWrittenYet)
                        {
                            buffer.Append("," + thrustDatas.GetData(true).burnTime.ToString("F7"));
                            buffer.Append("," + thrustDatas.GetData(true).maxThrust.ToString("F7"));
                            buffer.Append("," + thrustDatas.GetData(true).avgThrust.ToString("F7"));
                            buffer.Append("," + thrustDatas.GetData(true).impluse.ToString("F7"));
                            noDataWrittenYet = false;
                        }

                        await writer.WriteLineAsync(buffer.ToString());
                    }
                }

                MessageBox.Show("ファイルの出力が完了しました", "出力完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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

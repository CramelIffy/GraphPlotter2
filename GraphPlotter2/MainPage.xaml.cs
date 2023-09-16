using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Drawing;
using ScottPlot;

namespace GraphPlotter2
{
    /// <summary>
    /// MainPage.xaml の相互作用ロジック
    /// </summary>
    public partial class MainPage : Page
    {
        private readonly double plotMarginX;
        private readonly double plotMarginY;

        private readonly OpenFileDialog ofd;

        private readonly SaveFileDialog sfd;

        readonly DataModifier.DataModifier thrustDatas;
        public MainPage()
        {
            InitializeComponent();

            plotMarginX = 0.1;
            plotMarginY = 10;

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

            MainPlot.Plot.Clear();
            MainPlot.Plot.Title("TEST");
            MainPlot.Refresh();
        }

        private void PlotData()
        {
            int subGraphOpacity = MainWindow.SettingIO.Data.SubGraphOpacity * 255 / 100;
            int mainGraphUndenoisedOpacity = MainWindow.SettingIO.Data.UndenoisedGraphOpacity * 255 / 100;
            int burningOpacity = MainWindow.SettingIO.Data.BurningTimeOpacity * 255 / 100;
            MainPlot.Plot.Clear();
            // 燃焼時間を示すグラフを描画
            MainPlot.Plot.AddFill(
                thrustDatas.GetData(true).time.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray(),
                thrustDatas.GetData(true).thrust.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray()
                , 0, Color.FromArgb(burningOpacity, Color.Black));
            // サブグラフ描画
            try
            {
                var subGraph = MainPlot.Plot.AddSignalXY(thrustDatas.GetData(false).time, thrustDatas.GetData(false).denoisedThrust, Color.FromArgb(subGraphOpacity, Color.Black));
                subGraph.MarkerSize = 0;
            }
            catch (Exception ex)
            {

            };
            // メイングラフ描画
            MainPlot.Plot.AddSignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).thrust, Color.FromArgb(mainGraphUndenoisedOpacity, Color.Black));
            var mainGraph = MainPlot.Plot.AddSignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).denoisedThrust, Color.Black);
            mainGraph.LineWidth = 2;
            mainGraph.MarkerSize = 0;
            MainPlot.Plot.SetAxisLimitsX(thrustDatas.GetData(true).time[thrustDatas.GetData(true).ignitionIndex] - plotMarginX, thrustDatas.GetData(true).time[thrustDatas.GetData(true).burnoutIndex] + plotMarginX);
            MainPlot.Plot.SetAxisLimitsY(-plotMarginY, thrustDatas.GetData(true).maxThrust + plotMarginY);
            MainPlot.Refresh();
        }

        private void OpenCsv(object sender, RoutedEventArgs e)
        {
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    try
                    {
                        if (MainWindow.SettingIO.IsConfigFileExist())
                            MainWindow.SettingIO.LoadConfig();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("設定ファイルが読み込めません。\n" + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    thrustDatas.SetData(ofd.FileName, false, MainWindow.SettingIO.Data.IgnitionDetectionThreshold * 0.01, MainWindow.SettingIO.Data.BurnoutDetectionThreshold * 0.01);
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
                    try
                    {
                        if (MainWindow.SettingIO.IsConfigFileExist())
                            MainWindow.SettingIO.LoadConfig();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("設定ファイルが読み込めません。\n" + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    thrustDatas.SetData(ofd.FileName, true, MainWindow.SettingIO.Data.IgnitionDetectionThreshold * 0.01, MainWindow.SettingIO.Data.BurnoutDetectionThreshold * 0.01);
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
            }
            catch (Exception)
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
                        if (noDataWrittenYet)
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

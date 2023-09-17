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
using ScottPlot.Plottable;

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
            MainPlot.Plot.Title(MainWindow.SettingIO.Data.MainGraphName);
            MainPlot.Refresh();
        }

        private void AutoScaleForThrustCurve()
        {
            MainPlot.Plot.SetAxisLimitsX(thrustDatas.GetData(true).time[thrustDatas.GetData(true).ignitionIndex] - plotMarginX, thrustDatas.GetData(true).time[thrustDatas.GetData(true).burnoutIndex] + plotMarginX);
            MainPlot.Plot.SetAxisLimitsY(-plotMarginY, thrustDatas.GetData(true).maxThrust + plotMarginY);
        }

        private void PlotData()
        {
            int subGraphOpacity = MainWindow.SettingIO.Data.SubGraphOpacity * 255 / 100;
            int mainGraphUndenoisedOpacity = MainWindow.SettingIO.Data.UndenoisedGraphOpacity * 255 / 100;
            int burningOpacity = MainWindow.SettingIO.Data.BurningTimeOpacity * 255 / 100;
            MainPlot.Plot.Clear();
            MainPlot.Plot.XLabel("Time (s)");
            MainPlot.Plot.YLabel("Thrust (N)");
            // 燃焼時間を示すグラフを描画
            if (MainWindow.SettingIO.Data.BurningTime)
            {
                MainPlot.Plot.AddFill(
                    thrustDatas.GetData(true).time.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray(),
                    thrustDatas.GetData(true).thrust.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray()
                    , 0, Color.FromArgb(burningOpacity, Color.Black));

                var burnoutLine = MainPlot.Plot.AddVerticalLine(thrustDatas.GetData(true).burnTime, Color.DarkRed, 2, LineStyle.Dot);
                burnoutLine.Max = thrustDatas.GetData(true).thrust[thrustDatas.GetData(true).burnoutIndex];
                burnoutLine.PositionLabel = true;
                burnoutLine.PositionLabelBackground = burnoutLine.Color;
            }
            // サブグラフ描画
            if (MainWindow.SettingIO.Data.SubGraph)
                try
                {
                    var subGraph = MainPlot.Plot.AddSignalXY(thrustDatas.GetData(false).time, thrustDatas.GetData(false).denoisedThrust, Color.FromArgb(subGraphOpacity, Color.Black));
                    subGraph.MarkerSize = 0;
                }
                catch (Exception)
                {

                };
            // メイングラフ描画
            if (MainWindow.SettingIO.Data.MainGraph)
            {
                MainPlot.Plot.AddSignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).thrust, Color.FromArgb(mainGraphUndenoisedOpacity, Color.Black));
                var mainGraph = MainPlot.Plot.AddSignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).denoisedThrust, Color.Black);
                mainGraph.LineWidth = 2;
                mainGraph.MarkerSize = 0;
            }
            // 全力積描画
            if (MainWindow.SettingIO.Data.TotalImpulse)
            {
                var ano = MainPlot.Plot.AddAnnotation(MainWindow.SettingIO.Data.MainGraphName + ": " + thrustDatas.GetData(true).impluse.ToString("F3") + "N·s", Alignment.UpperRight);
                ano.MarginY = 10;
                ano.Font.Size = 24;
                ano.Shadow = false;
                ano.BackgroundColor = Color.White;
                if (MainWindow.SettingIO.Data.SubGraph)
                    try
                    {
                        string anoStr = MainWindow.SettingIO.Data.SubGraphName + ": " + thrustDatas.GetData(false).impluse.ToString("F3") + "N·s";
                        var anoSub = MainPlot.Plot.AddAnnotation(anoStr, Alignment.UpperRight);
                        anoSub.MarginY = 55;
                        anoSub.Font.Size = 24;
                        anoSub.Shadow = false;
                        anoSub.BackgroundColor = Color.White;
                    }
                    catch (Exception)
                    {

                    }
            }
            // 最大推力描画
            if (MainWindow.SettingIO.Data.MaxThrust)
            {
                double maxThrust = thrustDatas.GetData(true).maxThrust;
                double maxTime = thrustDatas.GetData(true).time[Array.IndexOf(thrustDatas.GetData(true).thrust, maxThrust)];
                var maxLine = MainPlot.Plot.AddHorizontalLine(maxThrust, Color.FromArgb(255, Color.Navy), 2, LineStyle.Dot);
                maxLine.Max = maxTime;
                maxLine.PositionLabel = true;
                maxLine.PositionLabelBackground = Color.FromArgb(180, Color.Navy);
                maxLine.PositionFormatter = x => $"max: \n{x:F2}";
            }
            // 平均推力描画
            if (MainWindow.SettingIO.Data.AverageThrust)
            {
                var avgLine = MainPlot.Plot.AddHorizontalLine(thrustDatas.GetData(true).avgThrust, Color.FromArgb(255, 12, 12, 12), 1, LineStyle.Dash);
                avgLine.PositionLabel = true;
                avgLine.PositionLabelBackground = Color.FromArgb(180, 12, 12, 12);
                avgLine.PositionFormatter = x => $"avg: \n{x:F2}";
            }
            // 拡大縮小
            AutoScaleForThrustCurve();
            MainPlot.Refresh();
        }

        private void OpenFile(bool isBinary)
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
                    double timePrefix;
                    double calibSlope;
                    double calibIntercept;
                    if (isBinary)
                    {
                        timePrefix = MainWindow.SettingIO.Data.PrefixOfTimeBIN;
                        calibSlope = MainWindow.SettingIO.Data.SlopeBIN;
                        calibIntercept = MainWindow.SettingIO.Data.InterceptBIN;
                    }
                    else
                    {
                        timePrefix = MainWindow.SettingIO.Data.PrefixOfTimeCSV;
                        calibSlope = MainWindow.SettingIO.Data.SlopeCSV;
                        calibIntercept = MainWindow.SettingIO.Data.InterceptCSV;
                    }
                    thrustDatas.SetData(ofd.FileName, isBinary, MainWindow.SettingIO.Data.IgnitionDetectionThreshold * 0.01, MainWindow.SettingIO.Data.BurnoutDetectionThreshold * 0.01, timePrefix, calibSlope, calibIntercept);
                    PlotData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("読み込みに失敗しました。\nErrorMessage: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenCsv(object sender, RoutedEventArgs e)
        {
            OpenFile(false);
        }
        private void OpenBin(object sender, RoutedEventArgs e)
        {
            OpenFile(true);
        }

        private void InitScreen(object sender, RoutedEventArgs e)
        {
            thrustDatas.InitData(true);
            thrustDatas.InitData(false);
            MainPlot.Plot.Clear();
            MainPlot.Refresh();
        }

        private void SaveImage(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = "Plot.png";
            sfd.Filter = "PNGファイル(*.png)|*.png|JPGファイル(*.jpg)|*.jpg";
            sfd.Title = "保存先のファイルを選択してください";

            if (sfd.ShowDialog() == true)
            {
                MainPlot.Plot.SaveFig(sfd.FileName, 1280, 720, false, 4);
                MessageBox.Show("保存しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }

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

        private void InitScale(object sender, RoutedEventArgs e)
        {
            try
            {
                thrustDatas.GetData(true);
                AutoScaleForThrustCurve();
                MainPlot.Refresh();
            }catch (Exception)
            {

            }
        }
        private void OpenAbout(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("GraphPlotter V2.0.1\nMade by Fujie Riu\nTokushimaUniv. Rocket Project", "バージョン情報", MessageBoxButton.OK);
        }
        private void Exit(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("本当に終了しますか？", "終了", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) Application.Current.Shutdown();
        }
    }
}

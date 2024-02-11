using Microsoft.Win32;
using ScottPlot;
using ScottPlot.LayoutEngines;
using ScottPlot.WPF;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Color = ScottPlot.Color;

namespace GraphPlotter2
{
    /// <summary>
    /// MainPage.xaml の相互作用ロジック
    /// </summary>
    public partial class MainPage : Page
    {
        private readonly double plotMarginX;
        private readonly double plotMarginY;
        private Plot plot;

        private readonly OpenFileDialog ofd;

        private readonly SaveFileDialog sfd;

        readonly DataModifier.DataModifier thrustDatas;
        public MainPage()
        {
            InitializeComponent();

            plotMarginX = 0.1;
            plotMarginY = 20;

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

            plot = MainPlot.Plot;

            plot.Clear();;
            plot.Title(MainWindow.SettingIO.Data.MainGraphName, 32.0f);
            MainPlot.Refresh();
        }

        private void AutoScaleForThrustCurve()
        {
            plot.Axes.SetLimitsX(thrustDatas.GetData(true).time[thrustDatas.GetData(true).ignitionIndex] - plotMarginX, thrustDatas.GetData(true).time[thrustDatas.GetData(true).burnoutIndex] + plotMarginX);
            plot.Axes.SetLimitsY(-plotMarginY, thrustDatas.GetData(true).maxThrust + plotMarginY);
        }

        private void PlotData()
        {
            uint subGraphOpacity = (uint)(MainWindow.SettingIO.Data.SubGraphOpacity * 255 / 100);
            uint mainGraphUndenoisedOpacity = (uint)(MainWindow.SettingIO.Data.UndenoisedGraphOpacity * 255 / 100);
            uint burningOpacity = (uint)(MainWindow.SettingIO.Data.BurningTimeOpacity * 255 / 100);
            plot.Clear();
            plot.XLabel("Time (s)");
            plot.YLabel("Thrust (N)");
            
            // サブグラフ描画
            if (MainWindow.SettingIO.Data.SubGraph)
                try
                {
                    var subGraph = plot.Add.SignalXY(thrustDatas.GetData(false).time, thrustDatas.GetData(false).denoisedThrust, Color.FromARGB(subGraphOpacity << 24));
                }
                catch (Exception)
                {

                };
            // メイングラフ描画
            if (MainWindow.SettingIO.Data.MainGraph)
            {
                plot.Add.SignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).thrust, Color.FromARGB(mainGraphUndenoisedOpacity << 24));
                var mainGraph = plot.Add.SignalXY(thrustDatas.GetData(true).time, thrustDatas.GetData(true).denoisedThrust, Colors.Black);
                mainGraph.LineWidth = 2;
            }
            // 燃焼時間を示すグラフを描画
            if (MainWindow.SettingIO.Data.BurningTime)
            {
                var fillPlot = plot.Add.FillY(
                    thrustDatas.GetData(true).time.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray(),
                    Enumerable.Repeat(0.0, thrustDatas.GetData(true).time.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray().Length).ToArray(),
                    thrustDatas.GetData(true).denoisedThrust.Skip(thrustDatas.GetData(true).ignitionIndex).Take(thrustDatas.GetData(true).burnoutIndex - thrustDatas.GetData(true).ignitionIndex).ToArray()
                    );
                fillPlot.FillStyle.Color = Color.FromARGB(burningOpacity << 24);

                var burnoutLine = plot.Add.VerticalLine(thrustDatas.GetData(true).burnTime, 2, Color.FromARGB(Colors.DarkRed.ARGB & 0xBFFFFFFF), LinePattern.Dotted);
                //burnoutLine.X = thrustDatas.GetData(true).thrust[thrustDatas.GetData(true).burnoutIndex];
                burnoutLine.Text = "burn time: " + thrustDatas.GetData(true).burnTime.ToString("F2") + " [s]";
            }
            // 最大推力描画
            if (MainWindow.SettingIO.Data.MaxThrust)
            {
                double maxThrust = thrustDatas.GetData(true).maxThrust;
                double maxTime = thrustDatas.GetData(true).time[Array.IndexOf(thrustDatas.GetData(true).thrust, maxThrust)];
                var maxLine = plot.Add.HorizontalLine(maxThrust, 2, Color.FromARGB(Colors.Navy.ARGB & 0xBFFFFFFF), LinePattern.Dotted);
                //maxLine.Y = maxTime;
                maxLine.Text = "max: " + maxThrust.ToString("F2") + " [N]";
            }
            // 平均推力描画
            if (MainWindow.SettingIO.Data.AverageThrust)
            {
                var avgLine = plot.Add.HorizontalLine(thrustDatas.GetData(true).avgThrust, 1, Color.FromARGB(0xBF0C0C0C), LinePattern.Dashed);
                avgLine.Text = "avg: " + thrustDatas.GetData(true).avgThrust.ToString("F2") + " [N]";
            }
            // 凡例の描画
            if (MainWindow.SettingIO.Data.TotalImpulse)
            {
                List<LegendItem> legends =
                [
                    new LegendItem { LineColor = Colors.Black, Marker = MarkerStyle.None, Label = MainWindow.SettingIO.Data.MainGraphName + ": " + thrustDatas.GetData(true).impluse.ToString("F3") + " [N·s]" },
                ];
                if (MainWindow.SettingIO.Data.SubGraph)
                    try
                    {
                        legends.Add(new LegendItem { LineColor = Colors.Black, Marker = MarkerStyle.None, Label = MainWindow.SettingIO.Data.SubGraphName + ": " + thrustDatas.GetData(false).impluse.ToString("F3") + " [N·s]" });
                    }
                    catch (Exception)
                    {
                    };
                plot.HideLegend();
                plot.ShowLegend(legends);
                plot.Legend.Location = Alignment.UpperRight;
                plot.Legend.Font.Size = 20;
            }
            // 拡大縮小
            AutoScaleForThrustCurve();
            MainPlot.Refresh();
        }

        private async void OpenFile(bool isBinary)
        {
            ofd.Filter = isBinary ? "GraphPlotter2用バイナリデータ (*.gpb)|*.gpb|すべてのファイル (*.*)|*.*" : "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*";
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
                    await thrustDatas.SetData(ofd.FileName, isBinary, MainWindow.SettingIO.Data.IgnitionDetectionThreshold * 0.01, MainWindow.SettingIO.Data.BurnoutDetectionThreshold * 0.01, timePrefix, calibSlope, calibIntercept, (int)(timePrefix * -980003 + 1001), 4);
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
            if (MessageBox.Show("本当にグラフを初期化しても良いですか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;
            thrustDatas.InitData(true);
            thrustDatas.InitData(false);
            plot.Clear();
            MainPlot.Refresh();
        }

        private void SaveImage(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new();
            sfd.FileName = "Plot.png";
            sfd.Filter = "PNGファイル(*.png)|*.png|SVGファイル(*.svg)|*.svg";
            sfd.Title = "保存先のファイルを選択してください";

            if (sfd.ShowDialog() == true)
            {
                if (Path.GetExtension(sfd.FileName) == ".svg")
                    plot.SaveSvg(sfd.FileName, 1920, 1080);
                else
                    plot.SavePng(sfd.FileName, 1920, 1080);
                MessageBox.Show("保存しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }

        }

        private async void SaveDataAsCsv(object sender, RoutedEventArgs e)
        {
            int startIndex;
            int endIndex;

            DataModifier.DataSet dataForWrite;

            try
            {
                dataForWrite = thrustDatas.GetData(true);
            }
            catch (Exception)
            {
                MessageBox.Show("データが読み込まれていません", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show("データの出力範囲は燃焼中のみで良いですか？\n(" + MessageBoxResult.No.ToString() + "を押すと全データが出力されます)", "確認", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                startIndex = dataForWrite.ignitionIndex;
                endIndex = dataForWrite.burnoutIndex;
            }
            else
            {
                startIndex = 0;
                endIndex = dataForWrite.time.Length - 1;
            }

            if (sfd.ShowDialog() == true)
            {
                ProgressBar progressBar = new((uint)(endIndex - startIndex + 1));
                string filePath = sfd.FileName;

                progressBar.UpdateStatus("Saving file…");
                progressBar.UpdateProgress(0);
                progressBar.Show();

                using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                using (StreamWriter writer = new(fileStream, Encoding.UTF8, 4096))
                {
                    await writer.WriteLineAsync("time(s),thrust(N),denoisedThrust(N),burningTime(s),maxThrust(N),averageThrust(N),totalImpluse(N·s)");

                    bool noDataWrittenYet = true;
                    StringBuilder buffer = new();
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        buffer.Clear();
                        buffer.Append(dataForWrite.time[i].ToString("F7"));
                        buffer.Append("," + dataForWrite.thrust[i].ToString("F7"));
                        buffer.Append("," + dataForWrite.denoisedThrust[i].ToString("F7"));
                        if (noDataWrittenYet)
                        {
                            buffer.Append("," + dataForWrite.burnTime.ToString("F7"));
                            buffer.Append("," + dataForWrite.maxThrust.ToString("F7"));
                            buffer.Append("," + dataForWrite.avgThrust.ToString("F7"));
                            buffer.Append("," + dataForWrite.impluse.ToString("F7"));
                            noDataWrittenYet = false;
                        }

                        await writer.WriteLineAsync(buffer.ToString());
                        progressBar.UpdateProgress(i - startIndex + 1);
                    }
                }

                progressBar.Close();

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
            }
            catch (Exception)
            {

            }
        }
        private void OpenAbout(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("GraphPlotter V2.1.2\nMade by Fujie Riu\nTokushimaUniv. Rocket Project", "バージョン情報", MessageBoxButton.OK);
        }
    }
}

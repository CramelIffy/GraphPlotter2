using GraphPlotter2;
using MathNet.Numerics.LinearAlgebra;
using System.CodeDom;
using System.Linq;
using System.Windows;

namespace DataModifier
{
    class DataSet
    {
        internal double[] time;         //s
        internal double[] thrust;       //N
        internal double[] denoisedThrust;//N
        internal int ignitionIndex;     //
        internal int burnoutIndex;      //
        internal double burnTime;       //s
        internal double maxThrust;      //N
        internal double avgThrust;      //N
        internal double impluse;            //N･s

        public DataSet()
        {
            time = Array.Empty<double>();
            thrust = Array.Empty<double>();
            denoisedThrust = Array.Empty<double>();
            ignitionIndex = 0;
            burnoutIndex = 0;
            burnTime = 0;
            maxThrust = 0;
            avgThrust = 0;
            impluse = 0;
        }
    }

    /// <summary>
    /// ロケットの推力データを受け取り、加工する。
    /// </summary>
    internal partial class DataModifier
    {
        private DataSet? mainData;
        private DataSet? subData;

        private Exception NumOfElementIsTooSmall;
        private Exception BurningTimeEstimationFailed;

        public DataModifier()
        {
            mainData = null;
            subData = null;

            NumOfElementIsTooSmall = new Exception("The number of data read is too small");
            BurningTimeEstimationFailed = new Exception("Burn time estimation failed");

        }

        public void InitData(bool isMain)
        {
            if (isMain)
                mainData = null;
            else
                subData = null;
        }

        public async Task SetData(string filePath, bool isBinary, double ignitionDetectionThreshold, double burnoutDetectionThreshold, double timePrefix, double calibSlope, double calibIntercept, int sidePoints, int polynomialOrder)
        {
            const int requireDetectionCount = 20;
            const int iterMax = 100;

            GraphPlotter2.ProgressBar progressBar = new ProgressBar(14);

            progressBar.UpdateStatus("Loading");
            progressBar.Show();

            try
            {
                var signalProc = await Task.Run(async () =>
                {
                    List<Task<MessageBoxResult>> messageBoxes = new();
                    // データ読み込み
                    progressBar.UpdateStatus("File Reading");
                    (List<(double Time, double Data)>, Exception?, bool isSomeDataCannotRead, bool isClockBack) decodedData = isBinary ? DecodeBinary(filePath, timePrefix, calibSlope, calibIntercept) : DecodeCSV(filePath, timePrefix, calibSlope, calibIntercept);
                    if (decodedData.Item2 != null)
                        throw decodedData.Item2;
                    if (decodedData.isSomeDataCannotRead)
                        messageBoxes.Add(Task.Run(() => MessageBox.Show("一部読み込めないデータが存在しています。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning)));
                    if (decodedData.isClockBack)
                        messageBoxes.Add(Task.Run(() => MessageBox.Show("時間逆行が発生している箇所があります。\n修正して出力します。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning)));

                    progressBar.IncreaseProgress();

                    // 時間データの逆行補正
                    progressBar.UpdateStatus("Time Reversal Correction");
                    // InsertionSort(tempData.time, tempData.thrust);
                    if (decodedData.isClockBack)
                        decodedData.Item1 = decodedData.Item1.AsParallel().OrderBy(data => data.Time).ToList();

                    progressBar.IncreaseProgress();

                    // 同一時間データが存在するときは平均を取る
                    progressBar.UpdateStatus("Same Timestamp Data Modification");
                    if (MainWindow.SettingIO.Data.AverageDuplicateTimestamps)
                        for (int i = 1; i < decodedData.Item1.Count; i++)
                        {
                            if (decodedData.Item1[i] != decodedData.Item1[i - 1])
                                continue;
                            double newData = decodedData.Item1[i - 1].Data;
                            uint count = 1;
                            while (i < decodedData.Item1.Count && decodedData.Item1[i] == decodedData.Item1[i - 1])
                            {
                                newData += decodedData.Item1[i].Data;
                                decodedData.Item1.RemoveAt(i);
                                count++;
                            }
                            decodedData.Item1[i - 1] = (decodedData.Item1[i - 1].Time, newData / count);
                        }

                    progressBar.IncreaseProgress();

                    // オフセット除去
                    progressBar.UpdateStatus("Offset Removal");
                    {
                        var sortedDecodedDataItem1 = decodedData.Item1.AsParallel().Select(x => x.Data).ToArray();
                        Array.Sort(sortedDecodedDataItem1);
                        progressBar.UpdatePercentProgress(50);
                        double thrustOffset = sortedDecodedDataItem1.Skip((int)(decodedData.Item1.Count * 0.4)).Take((int)(decodedData.Item1.Count * 0.2)).AsParallel().Average();
                        decodedData.Item1 = decodedData.Item1.AsParallel().AsOrdered().Select(x => (x.Time, x.Data - thrustOffset)).ToList();
                    }

                    progressBar.IncreaseProgress();

                    DataSet tempData = new();

                    int iterCount = 0;

                    // フィルタ構築
                    progressBar.UpdateStatus("Filter Construction");
                    SignalFilter.SignalFilter filter = new(sidePoints, polynomialOrder);

                    progressBar.IncreaseProgress();

                    int filterStartIdx = 0;
                    int filterEndIdx = decodedData.Item1.Count;

                    // うまく読み込めるまでループ
                    while (++iterCount <= iterMax)
                    {
                        // 異常チェック
                        progressBar.UpdateStatus("Data Anomaly Check");
                        if (decodedData.Item1.Count <= requireDetectionCount * 2 + iterMax)
                            throw NumOfElementIsTooSmall;

                        progressBar.IncreaseProgress();

                        // 時間、推力データを配列に変換
                        progressBar.UpdateStatus("Analysis Preparation");
                        tempData.time = decodedData.Item1.Select(item => item.Time).ToArray();
                        progressBar.UpdatePercentProgress(50);
                        tempData.thrust = decodedData.Item1.Select(item => item.Data).ToArray();
                        if (tempData.denoisedThrust.Length == 0)
                            tempData.denoisedThrust = new double[tempData.thrust.Length];

                        progressBar.IncreaseProgress();

                        // ノイズ除去計算
                        progressBar.UpdateStatus("Noise Reduction");
                        Array.Copy(filter.Process(tempData.thrust, filterStartIdx, filterEndIdx), filterStartIdx, tempData.denoisedThrust, filterStartIdx, filterEndIdx - filterStartIdx);

                        progressBar.IncreaseProgress();

                        // 最大値計算
                        progressBar.UpdateStatus("Maximum Thrust Calculation");
                        tempData.maxThrust = tempData.thrust.Max();

                        progressBar.IncreaseProgress();

                        // 燃焼時間推定開始
                        progressBar.UpdateStatus("Burn Time Estimation");
                        // インデックスの初期化
                        tempData.ignitionIndex = 0;
                        tempData.burnoutIndex = tempData.thrust.Length - 1;
                        int maxThrustIndex = Array.IndexOf(tempData.thrust, tempData.maxThrust);
                        // 推定開始
                        int[] detectCount = { 0, 0 };
                        Parallel.Invoke(() =>
                        {
                            for (int i = maxThrustIndex; i >= 0; i--)
                                if (tempData.denoisedThrust[i] < tempData.maxThrust * ignitionDetectionThreshold)
                                {
                                    detectCount[0]++;
                                    if (detectCount[0] == requireDetectionCount)
                                    {
                                        tempData.ignitionIndex = i + (requireDetectionCount - 1);
                                        break;
                                    }
                                }
                                else
                                    detectCount[0] = 0;
                        }, () =>
                        {
                            for (int i = maxThrustIndex; i < tempData.thrust.Length; i++)
                                if (tempData.denoisedThrust[i] < tempData.maxThrust * burnoutDetectionThreshold)
                                {
                                    detectCount[1]++;
                                    if (detectCount[1] == requireDetectionCount)
                                    {
                                        tempData.burnoutIndex = i - (requireDetectionCount - 1);
                                        break;
                                    }
                                }
                                else
                                    detectCount[1] = 0;
                        });
                        // 燃焼していないデータ数が少ないときの処理
                        if (detectCount[0] != requireDetectionCount)
                            tempData.ignitionIndex += detectCount[0];
                        if (detectCount[1] != requireDetectionCount)
                            tempData.burnoutIndex -= detectCount[1];

                        // 燃焼時間推定が成功したかどうかの判定
                        if (tempData.ignitionIndex == tempData.burnoutIndex)
                        {
                            progressBar.UpdateStatus("In Preparation for Retry");
                            progressBar.UpdateProgress(5 - 1);
                            decodedData.Item1.RemoveAt(maxThrustIndex);
                            progressBar.UpdatePercentProgress(50);
                            tempData.denoisedThrust = tempData.denoisedThrust.AsParallel().AsOrdered().Where((source, index) => index != maxThrustIndex).ToArray();
                            filterStartIdx = maxThrustIndex - sidePoints - 1;
                            filterEndIdx = maxThrustIndex + sidePoints + 1;
                            if (filterStartIdx < 0)
                            {
                                filterEndIdx -= filterStartIdx;
                                filterStartIdx = 0;
                            }
                            if (filterEndIdx >= decodedData.Item1.Count)
                            {
                                filterStartIdx -= filterEndIdx - decodedData.Item1.Count;
                                filterEndIdx = decodedData.Item1.Count - 1;
                            }
                            if (filterStartIdx < 0 || filterEndIdx >= decodedData.Item1.Count)
                                throw BurningTimeEstimationFailed;
                            progressBar.IncreaseProgress();
                        }
                        else
                            break;

                    }
                    if (iterCount == iterMax)
                        throw BurningTimeEstimationFailed;
                    if (iterCount > 1)
                        _ = Task.Run(() => MessageBox.Show("燃焼時間推定に失敗したため、\n計" + (iterCount - 1) + "個の外れ値と推定される値を削除しました。", "外れ値削除", MessageBoxButton.OK, MessageBoxImage.Information));

                    tempData.burnTime = tempData.time[tempData.burnoutIndex] - tempData.time[tempData.ignitionIndex];
                    // 燃焼時間推定終了
                    progressBar.IncreaseProgress();

                    // 燃焼開始地点を0に移動
                    progressBar.UpdateStatus("Combustion Start Time Change to Zero Seconds");
                    {
                        double ignitionTime = tempData.time[tempData.ignitionIndex];
                        tempData.time = tempData.time.AsParallel().Select(x => x - ignitionTime).ToArray();
                    }
                    progressBar.IncreaseProgress();

                    // 平均推力計算
                    progressBar.UpdateStatus("Average Thrust Calculation");
                    tempData.avgThrust = tempData.thrust.Skip(tempData.ignitionIndex).Take(tempData.burnoutIndex - tempData.ignitionIndex).Average();

                    progressBar.IncreaseProgress();

                    // 力積の計算
                    progressBar.UpdateStatus("Total Impulse Calculation");
                    var impulseTemp = new double[tempData.burnoutIndex - tempData.ignitionIndex + 1];
                    Parallel.For(tempData.ignitionIndex + 1, tempData.burnoutIndex + 1, i =>
                    {
                        impulseTemp[i - tempData.ignitionIndex - 1] = (tempData.thrust[i] + tempData.thrust[i - 1]) * (tempData.time[i] - tempData.time[i - 1]);
                    });
                    progressBar.UpdatePercentProgress(50);
                    Array.Sort(impulseTemp); // 極端に大きな値と小さな値が混在するとき、有効数字の関係で小さい値が消えてしまうことがあるためソート(おそらく不必要だとは思われるが一応)
                    tempData.impluse = impulseTemp.Sum() / 2;

                    progressBar.IncreaseProgress();

                    if (mainData == null)
                        mainData = tempData;
                    else
                        subData = tempData;

                    // 完了
                    progressBar.UpdateStatus("Complete");
                    progressBar.IncreaseProgress();
                    await Task.Delay(200);

                    foreach (var messageBox in messageBoxes)
                        _ = await messageBox;

                    return true;
                });
            }
            catch (Exception)
            {
                progressBar.Close();
                throw;
            }

            progressBar.Close();
        }

        public DataSet GetData(bool isMain)
        {
            DataSet? temp = isMain ? mainData : subData;
            if (temp != null)
                return temp;
            else
                throw new InvalidOperationException();
        }
    }
}

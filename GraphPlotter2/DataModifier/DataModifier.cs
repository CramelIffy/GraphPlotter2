﻿using MathNet.Numerics.LinearAlgebra;
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

        private static void InsertionSort(double[] time, double[] thrust)
        {
            int length = time.Length;

            for (int i = 1; i < length; i++)
            {
                double currentTime = time[i];
                double currentThrust = thrust[i];
                int j = i - 1;

                while (j >= 0 && time[j] > currentTime)
                {
                    time[j + 1] = time[j];
                    thrust[j + 1] = thrust[j];
                    j--;
                }

                time[j + 1] = currentTime;
                thrust[j + 1] = currentThrust;
            }
        }

        public async Task SetData(string filePath, bool isBinary, double ignitionDetectionThreshold, double burnoutDetectionThreshold, double timePrefix, double calibSlope, double calibIntercept, int sidePoints, int polynomialOrder)
        {
            const int requireDetectionCount = 20;
            const int iterMax = 20;

            GraphPlotter2.ProgressBar progressBar = new(14);
            progressBar.UpdateStatus("Loading");
            progressBar.Show();

            await Task.Run(async () =>
            {
                // データ読み込み
                progressBar.UpdateStatus("File Reading");
                progressBar.IncreaseProgress();
                (List<(double Time, double Data)>, Exception?, bool isSomeDataCannotRead, bool isClockBack) decodedData = isBinary ? DecodeBinary(filePath, timePrefix, calibSlope, calibIntercept) : DecodeCSV(filePath, timePrefix, calibSlope, calibIntercept);
                if (decodedData.Item2 != null)
                    throw decodedData.Item2;
                if (decodedData.isSomeDataCannotRead)
                    MessageBox.Show("一部読み込めないデータが存在しています。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (decodedData.isClockBack)
                    MessageBox.Show("時間逆行が発生している箇所があります。\n修正して出力します。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);

                DataSet tempData = new();

                int iterCount = 0;

                // フィルタ構築
                progressBar.UpdateStatus("Filter Construction");
                progressBar.IncreaseProgress();
                SavitzkyGolayFilter filter = new(sidePoints, polynomialOrder);

                // うまく読み込めるまでループ
                while (++iterCount <= iterMax)
                {
                    // 異常チェック
                    progressBar.UpdateStatus("Data Anomaly Check");
                    progressBar.IncreaseProgress();
                    if (decodedData.Item1.Count <= requireDetectionCount * 2 + iterMax)
                        throw NumOfElementIsTooSmall;
                    // 時間データの逆行補正
                    progressBar.UpdateStatus("Time Reversal Correction");
                    progressBar.IncreaseProgress();
                    // InsertionSort(tempData.time, tempData.thrust);
                    decodedData.Item1 = decodedData.Item1.AsParallel().OrderBy(data => data.Time).ToList();
                    // 同一時間データが存在するときは平均を取る
                    progressBar.UpdateStatus("Same Timestamp Data Modification");
                    progressBar.IncreaseProgress();
                    if (decodedData.Item1.AsParallel().GroupBy(data => data.Time).Any(group => group.Count() > 1))
                        decodedData.Item1 = decodedData.Item1.AsParallel().GroupBy(data => data.Time).Select(group => (Time: group.Key, Data: group.Average(tuple => tuple.Data))).ToList();
                    // 時間、推力データを配列に変換
                    progressBar.UpdateStatus("Analysis Preparation");
                    progressBar.IncreaseProgress();
                    tempData.time = decodedData.Item1.Select(item => item.Time).ToArray();
                    tempData.thrust = decodedData.Item1.Select(item => item.Data).ToArray();
                    // オフセット除去
                    progressBar.UpdateStatus("Offset Removal");
                    progressBar.IncreaseProgress();
                    double thrustOffset = tempData.thrust.OrderBy(x => x).Skip((int)(tempData.thrust.Length * 0.1) * 2).Take((int)(tempData.thrust.Length * 0.1) + 1).Average();
                    tempData.thrust = tempData.thrust.AsParallel().Select(x => x - thrustOffset).ToArray();
                    // ノイズ除去計算
                    progressBar.UpdateStatus("Noise Reduction");
                    progressBar.IncreaseProgress();
                    tempData.denoisedThrust = filter.Process(tempData.thrust);
                    // 最大値計算
                    progressBar.UpdateStatus("Maximum Thrust Calculation");
                    progressBar.IncreaseProgress();
                    tempData.maxThrust = tempData.thrust.Max();

                    // 燃焼時間推定開始
                    progressBar.UpdateStatus("Burn Time Estimation");
                    progressBar.IncreaseProgress();
                    // インデックスの初期化
                    tempData.ignitionIndex = 0;
                    tempData.burnoutIndex = tempData.thrust.Length - 1;
                    int maxThrustIndex = Array.IndexOf(tempData.thrust, tempData.maxThrust);

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
                        if (iterMax - iterCount != 0)
                            if (MessageBox.Show("燃焼時間推定に失敗しました。\n再挑戦しますか。\n(残り再挑戦可能回数: " + (iterMax - iterCount) + ")", "エラー", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                                throw BurningTimeEstimationFailed;
                        decodedData.Item1.RemoveAt(maxThrustIndex);
                    }
                    else
                        break;

                }
                if (iterCount == iterMax)
                    throw BurningTimeEstimationFailed;

                tempData.burnTime = tempData.time[tempData.burnoutIndex] - tempData.time[tempData.ignitionIndex];
                // 燃焼時間推定終了

                // 燃焼開始地点を0に移動
                progressBar.UpdateStatus("Combustion Start Time Change to Zero Seconds");
                progressBar.IncreaseProgress();
                {
                    double ignitionTime = tempData.time[tempData.ignitionIndex];
                    tempData.time = tempData.time.AsParallel().Select(x => x - ignitionTime).ToArray();
                }

                // 平均推力計算
                progressBar.UpdateStatus("Average Thrust Calculation");
                progressBar.IncreaseProgress();
                tempData.avgThrust = tempData.thrust.Skip(tempData.ignitionIndex).Take(tempData.burnoutIndex - tempData.ignitionIndex).Average();

                // 力積の計算
                progressBar.UpdateStatus("Total Impulse Calculation");
                progressBar.IncreaseProgress();
                var impulseTemp = new double[tempData.burnoutIndex - tempData.ignitionIndex + 1];
                Parallel.For(tempData.ignitionIndex + 1, tempData.burnoutIndex + 1, i =>
                {
                    impulseTemp[i - tempData.ignitionIndex - 1] = (tempData.thrust[i] + tempData.thrust[i - 1]) * (tempData.time[i] - tempData.time[i - 1]);
                });
                Array.Sort(impulseTemp); // 極端に大きな値と小さな値が混在するとき、有効数字の関係で小さい値が消えてしまうことがあるためソート(おそらく不必要だとは思われるが一応)
                tempData.impluse = impulseTemp.Sum() / 2;

                if (mainData == null)
                    mainData = tempData;
                else
                    subData = tempData;

                // 完了
                progressBar.UpdateStatus("Complete");
                progressBar.IncreaseProgress();
                await Task.Delay(200);
            });

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

    /// <summary>
    /// <para>Implements a Savitzky-Golay smoothing filter, as found in [1].</para>
    /// <para>[1] Sophocles J.Orfanidis. 1995. Introduction to Signal Processing. Prentice-Hall, Inc., Upper Saddle River, NJ, USA.</para>
    /// </summary>
    internal sealed class SavitzkyGolayFilter
    {
        private readonly int sidePoints;

        private Matrix<double> coefficients;

        public SavitzkyGolayFilter(int sidePoints, int polynomialOrder)
        {
            this.sidePoints = sidePoints;
            double[,] a = new double[(sidePoints << 1) + 1, polynomialOrder + 1];

            Parallel.For(-sidePoints, sidePoints + 1, m =>
            {
                for (int i = 0; i <= polynomialOrder; ++i)
                {
                    a[m + sidePoints, i] = Math.Pow(m, i);
                }
            });

            Matrix<double> s = Matrix<double>.Build.DenseOfArray(a);
            coefficients = s.Multiply(s.TransposeThisAndMultiply(s).Inverse()).Multiply(s.Transpose());
        }

        /// <summary>
        /// Smoothes the input samples.
        /// </summary>
        /// <param name="samples"></param>
        /// <returns></returns>
        public double[] Process(double[] samples)
        {
            int length = samples.Length;
            double[] output = new double[length];
            int frameSize = (sidePoints << 1) + 1;
            double[] frame = new double[frameSize];

            Parallel.Invoke(() =>
            {
                double[] frame = new double[frameSize];
                Array.Copy(samples, frame, frameSize);
                for (int i = 0; i < sidePoints; ++i)
                {
                    output[i] = coefficients.Column(i).DotProduct(Vector<double>.Build.DenseOfArray(frame));
                }
            }, () =>
            {
                double[] frame = new double[frameSize];
                for (int n = sidePoints; n < length - sidePoints; ++n)
                {
                    Array.ConstrainedCopy(samples, n - sidePoints, frame, 0, frameSize);
                    output[n] = coefficients.Column(sidePoints).DotProduct(Vector<double>.Build.DenseOfArray(frame));
                }
            });

            Array.ConstrainedCopy(samples, length - frameSize, frame, 0, frameSize);

            for (int i = 0; i < sidePoints; ++i)
            {
                output[length - sidePoints + i] = coefficients.Column(sidePoints + 1 + i).DotProduct(Vector<double>.Build.Dense(frame));
            }

            return output;
        }
    }
}

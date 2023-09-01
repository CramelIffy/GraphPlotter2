using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

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

            NumOfElementIsTooSmall = new Exception("The number of elements is too small");
            BurningTimeEstimationFailed = new Exception("Burn time estimation failed");
        }

        public void InitData(bool isMain)
        {
            if (isMain)
                mainData = null;
            else
                subData = null;
        }

        public void SetData(string filePath, bool isBinary, double ignitionDetectionThreshold = 0.05, double burnoutDetectionThreshold = 0.05, int sidePoints = 21, int polynomialOrder = 4)
        {
            const int requireDetectionCount = 20;
            const int iterMax = 20;

            // データ読み込み
            (List<(double Time, double Data)>, Exception?) decodedData = isBinary ? DecodeBinary(filePath) : DecodeCSV(filePath);
            if (decodedData.Item2 != null)
                throw decodedData.Item2;

            DataSet tempData = new();

            int iterCount = 0;

            SavitzkyGolayFilter filter = new(sidePoints, polynomialOrder);

            while (++iterCount <= iterMax)
            {
                // 時間、推力データを配列に変換
                tempData.time = decodedData.Item1.Select(item => item.Time).ToArray();
                tempData.thrust = decodedData.Item1.Select(item => item.Data).ToArray();
                if (tempData.thrust.Length <= requireDetectionCount * 2 + iterMax)
                    throw NumOfElementIsTooSmall;
                // オフセット除去
                double thrustOffset = tempData.thrust.OrderBy(x => x).Skip((int)(tempData.thrust.Length * 0.01)).Take((int)(tempData.thrust.Length * 0.01) + 1).Average();
                tempData.thrust = tempData.thrust.AsParallel().Select(x => x - thrustOffset).ToArray();
                // ノイズ除去計算
                tempData.denoisedThrust = filter.Process(tempData.thrust);

                // インデックスの初期化
                tempData.ignitionIndex = 0;
                tempData.burnoutIndex = tempData.thrust.Length - 1;

                // 最大値計算
                tempData.maxThrust = tempData.thrust.Max();

                // 燃焼時間推定
                int maxThrustIndex = Array.IndexOf(tempData.thrust, tempData.maxThrust);
                Parallel.Invoke(() =>
                {
                    int detectCount = 0;
                    for (int i = maxThrustIndex; i >= 0; i--)
                        if (tempData.denoisedThrust[i] < tempData.maxThrust * ignitionDetectionThreshold)
                        {
                            detectCount++;
                            if (detectCount >= requireDetectionCount)
                            {
                                tempData.ignitionIndex = i + (requireDetectionCount - 1);
                                break;
                            }

                        }
                        else
                            detectCount = 0;
                }, () =>
                {
                    int detectCount = 0;
                    for (int i = maxThrustIndex; i < tempData.thrust.Length; i++)
                        if (tempData.denoisedThrust[i] < tempData.maxThrust * burnoutDetectionThreshold)
                        {
                            detectCount++;
                            if (detectCount >= requireDetectionCount)
                            {
                                tempData.burnoutIndex = i - (requireDetectionCount - 1);
                                break;
                            }

                        }
                        else
                            detectCount = 0;
                });
                if (tempData.ignitionIndex == tempData.burnoutIndex)
                    decodedData.Item1.RemoveAt(maxThrustIndex);
                else if (tempData.ignitionIndex == 0 && tempData.burnoutIndex == tempData.thrust.Length - 1)
                    throw NumOfElementIsTooSmall;
                else
                    break;

            }
            if (iterCount == iterMax)
                throw BurningTimeEstimationFailed;

            // 燃焼時間計算
            tempData.burnTime = tempData.time[tempData.burnoutIndex] - tempData.time[tempData.ignitionIndex];

            // 平均推力計算
            tempData.avgThrust = tempData.thrust.Skip(tempData.ignitionIndex).Take(tempData.burnoutIndex - tempData.ignitionIndex).Average();

            // 力積の計算
            for (int i = tempData.ignitionIndex; i < tempData.burnoutIndex; i++)
                tempData.impluse += (tempData.thrust[i + 1] + tempData.thrust[i]) * (tempData.time[i + 1] - tempData.time[i]);
            tempData.impluse /= 2;

            if (mainData == null)
                mainData = tempData;
            else
                subData = tempData;
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

            Array.Copy(samples, frame, frameSize);

            Parallel.Invoke(() =>
            {
                for (int i = 0; i < sidePoints; ++i)
                {
                    output[i] = coefficients.Column(i).DotProduct(Vector<double>.Build.DenseOfArray(frame));
                }
            }, () =>
            {
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

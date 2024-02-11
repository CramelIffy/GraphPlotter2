using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalFilter
{
    /// <summary>
    /// <para>Implements a Savitzky-Golay smoothing filter, as found in [1].</para>
    /// <para>[1] Sophocles J.Orfanidis. 1995. Introduction to Signal Processing. Prentice-Hall, Inc., Upper Saddle River, NJ, USA.</para>
    /// </summary>
    internal sealed class SignalFilter
    {
        private readonly int sidePoints;
        private System.Numerics.Vector<double>[][] coefficients;
        private int vectorSize;

        public SignalFilter(int sidePoints, int polynomialOrder)
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

            int trialCount = 0;
            for (; trialCount < 10; trialCount++)
            {
                vectorSize = System.Numerics.Vector<double>.Count;
                if (vectorSize != 0)
                    break;
            }
            if (trialCount == 10)
                throw new NotSupportedException();

            Matrix<double> s = Matrix<double>.Build.DenseOfArray(a);
            s = s.Multiply(s.TransposeThisAndMultiply(s).Inverse()).Multiply(s.Transpose());
            coefficients = new System.Numerics.Vector<double>[s.ColumnCount][];
            for (int i = 0; i < coefficients.Length; ++i)
            {
                var coefColumn = s.Column(i).ToArray();
                var coefColumnPart = new double[vectorSize];
                coefficients[i] = new System.Numerics.Vector<double>[(s.RowCount + vectorSize - 1) / vectorSize];
                for (int j = 0; j < coefficients[i].Length; ++j)
                {
                    int baseIndex = j * vectorSize;
                    int remainingItems = coefColumn.Length - baseIndex;
                    if (remainingItems > vectorSize)
                        remainingItems = vectorSize;
                    Array.Copy(coefColumn, baseIndex, coefColumnPart, 0, remainingItems);
                    if (coefColumnPart.Length - remainingItems != 0)
                        Array.Clear(coefColumnPart, remainingItems, coefColumnPart.Length - remainingItems);
                    coefficients[i][j] = new System.Numerics.Vector<double>(coefColumnPart);
                }
            }
        }

        public double[] Process(double[] samples, int startIndex = 0, int endIndex = int.MaxValue)
        {
            int length = samples.Length;
            if (startIndex < 0)
                startIndex = 0;
            if (endIndex >= length)
                endIndex = length - 1;
            if (endIndex <= startIndex + (sidePoints << 1))
                throw new ArgumentException("endIndex must be at least twice as large as sidePoints than startIndex.");
            double[] output = new double[length];
            int frameSize = (sidePoints << 1) + 1;
            var paddedLength = length + vectorSize - (length % vectorSize);
            double[] paddedSamples = new double[paddedLength];
            Array.Copy(samples, paddedSamples, length);

            int vectorCount = (frameSize + vectorSize - 1) / vectorSize;
            for (int i = startIndex; i < sidePoints; ++i)
            {
                System.Numerics.Vector<double> result = System.Numerics.Vector<double>.Zero;
                for (int vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
                {
                    int baseIndex = vectorIndex * vectorSize;
                    int remainingItems = frameSize - baseIndex;
                    if (remainingItems > vectorSize) remainingItems = vectorSize;
                    double[] tempVector = new double[vectorSize];
                    Array.Copy(paddedSamples, baseIndex, tempVector, 0, remainingItems);
                    result += System.Numerics.Vector.Multiply(new System.Numerics.Vector<double>(tempVector), coefficients[i][vectorIndex]);
                }
                output[i] = System.Numerics.Vector.Sum(result);
            }

            int midStartIdx = Math.Max(sidePoints, startIndex);
            int midEndIdx = Math.Min(length - sidePoints, endIndex + 1);
            // 中央部分の処理
            Parallel.For(midStartIdx, midEndIdx,
                () => (result: new System.Numerics.Vector<double>(), tempVector: new double[vectorSize]),
                (n, state, local) =>
                {
                    local.result = System.Numerics.Vector<double>.Zero;
                    int baseIndex = n - sidePoints - vectorSize;
                    int remainingItems;
                    Span<double> paddedSamplesSpan = new(paddedSamples);
                    Span<System.Numerics.Vector<double>> coefSpan = new(coefficients[sidePoints]);

                    for (int vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
                    {
                        baseIndex += vectorSize;
                        remainingItems = frameSize - vectorIndex * vectorSize;

                        if (remainingItems > vectorSize)
                            local.result += new System.Numerics.Vector<double>(paddedSamplesSpan.Slice(baseIndex, vectorSize)) * coefSpan[vectorIndex];
                        else
                        {
                            Array.Copy(paddedSamples, baseIndex, local.tempVector, 0, remainingItems);
                            Array.Clear(local.tempVector, remainingItems, vectorSize - remainingItems);
                            local.result += new System.Numerics.Vector<double>(local.tempVector) * coefSpan[vectorIndex];
                        }
                    }
                    output[n] = System.Numerics.Vector.Sum(local.result);
                    return local;
                },
                local => { }
            );

            // 末尾の処理
            for (int i = 0; i + length - sidePoints <= endIndex; ++i)
            {
                System.Numerics.Vector<double> result = System.Numerics.Vector<double>.Zero;
                for (int vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
                {
                    int baseIndex = length - frameSize + vectorIndex * vectorSize;
                    int remainingItems = frameSize - vectorIndex * vectorSize;
                    if (remainingItems > vectorSize) remainingItems = vectorSize;
                    double[] tempVector = new double[vectorSize];
                    Array.Copy(paddedSamples, baseIndex, tempVector, 0, remainingItems);
                    result += System.Numerics.Vector.Multiply(new System.Numerics.Vector<double>(tempVector), coefficients[sidePoints + 1 + i][vectorIndex]);
                }
                output[length - sidePoints + i] = System.Numerics.Vector.Sum(result);
            }

            return output;
        }
    }
}

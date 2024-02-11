using MathNet.Numerics.LinearAlgebra;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SignalFilter
{
    /// <summary>
    /// <para>Implements a Savitzky-Golay smoothing filter, as found in [1].</para>
    /// <para>[1] Sophocles J.Orfanidis. 1995. Introduction to Signal Processing. Prentice-Hall, Inc., Upper Saddle River, NJ, USA.</para>
    /// </summary>
    internal sealed class SignalFilter
    {
        private readonly int sidePoints;
        private readonly System.Numerics.Vector<double>[][] coefficients;
        private readonly Vector512<double>[][] coef512;
        private int vectorSize;
        private bool isAvx512Supported;

        public SignalFilter(int sidePoints, int polynomialOrder)
        {
            isAvx512Supported = Avx512F.IsSupported;
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
            s = s.Multiply(s.TransposeThisAndMultiply(s).Inverse()).Multiply(s.Transpose());

            if (isAvx512Supported)
            {
                vectorSize = 8;
                coef512 = new Vector512<double>[s.ColumnCount][];
                for (int i = 0; i < coef512.Length; i++)
                {
                    var coefColumn = s.Column(i).ToArray();
                    var coefColumnPart = new double[vectorSize];
                    coef512[i] = new Vector512<double>[(s.RowCount + vectorSize - 1) / vectorSize];
                    for (int j = 0; j < coef512[i].Length; ++j)
                    {
                        int baseIndex = j * vectorSize;
                        int remainingItems = coefColumn.Length - baseIndex;
                        if (remainingItems > vectorSize)
                            remainingItems = vectorSize;
                        Array.Copy(coefColumn, baseIndex, coefColumnPart, 0, remainingItems);
                        if (coefColumnPart.Length - remainingItems != 0)
                            Array.Clear(coefColumnPart, remainingItems, coefColumnPart.Length - remainingItems);
                        coef512[i][j] = ConvertArrayToVector512(ref coefColumnPart);
                    }
                }
                // NULL非許容体のための処理
                coefficients = new System.Numerics.Vector<double>[1][];
            }
            else
            {
                int trialCount = 0;
                for (; trialCount < 10; trialCount++)
                {
                    vectorSize = System.Numerics.Vector<double>.Count;
                    if (vectorSize != 0)
                        break;
                }
                if (trialCount == 10)
                    throw new NotSupportedException();

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
                // NULL非許容体のための処理
                coef512 = new Vector512<double>[1][];
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
            if (isAvx512Supported)
            {
                for (int i = startIndex; i < sidePoints; ++i)
                {
                    Vector512<double> result = Vector512<double>.Zero;
                    double[] tempVector = new double[vectorSize];
                    for (int vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
                    {
                        int baseIndex = vectorIndex * vectorSize;
                        int remainingItems = frameSize - baseIndex;
                        if (remainingItems > vectorSize) remainingItems = vectorSize;
                        Array.Copy(paddedSamples, baseIndex, tempVector, 0, remainingItems);
                        Array.Clear(tempVector, remainingItems, vectorSize - remainingItems);
                        result += ConvertArrayToVector512(ref tempVector) * coef512[i][vectorIndex];
                    }
                    output[i] = Vector512.Sum(result);
                }

                int midStartIdx = Math.Max(sidePoints, startIndex);
                int midEndIdx = Math.Min(length - sidePoints, endIndex + 1);
                // 中央部分の処理
                Parallel.For(midStartIdx, midEndIdx,
                    () => (result: new Vector512<double>(), tempVector: new double[vectorSize]),
                    (n, state, local) =>
                    {
                        local.result = Vector512<double>.Zero;
                        int baseIndex = n - sidePoints - vectorSize;
                        int remainingItems;
                        ReadOnlySpan<double> paddedSamplesSpan = new(paddedSamples);
                        ReadOnlySpan<Vector512<double>> coefSpan = new(coef512[sidePoints]);

                        for (int vectorIndex = 0; vectorIndex < vectorCount - 1; vectorIndex++)
                        {
                            baseIndex += vectorSize;
                            remainingItems = frameSize - vectorIndex * vectorSize;

                            local.result += ConvertReadOnlySpanToVector512(paddedSamplesSpan.Slice(baseIndex, vectorSize)) * coefSpan[vectorIndex];
                        }
                        baseIndex += vectorSize;
                        remainingItems = frameSize - (vectorCount - 1) * vectorSize;
                        var tempVectorSpan = new Span<double>(local.tempVector);
                        paddedSamplesSpan.Slice(baseIndex, remainingItems).CopyTo(tempVectorSpan);
                        tempVectorSpan.Slice(remainingItems).Clear();
                        local.result += ConvertReadOnlySpanToVector512<double>(tempVectorSpan) * coefSpan[vectorCount - 1];

                        output[n] = Vector512.Sum(local.result);
                        return local;
                    },
                    local => { }
                );

                // 末尾の処理
                for (int i = 0; i + length - sidePoints <= endIndex; ++i)
                {
                    Vector512<double> result = Vector512<double>.Zero;
                    double[] tempVector = new double[vectorSize];
                    for (int vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
                    {
                        int baseIndex = length - frameSize + vectorIndex * vectorSize;
                        int remainingItems = frameSize - vectorIndex * vectorSize;
                        if (remainingItems > vectorSize) remainingItems = vectorSize;
                        Array.Copy(paddedSamples, baseIndex, tempVector, 0, remainingItems);
                        Array.Clear(tempVector, remainingItems, vectorSize - remainingItems);
                        result += ConvertArrayToVector512(ref tempVector) * coef512[sidePoints + 1 + i][vectorIndex];
                    }
                    output[length - sidePoints + i] = Vector512.Sum(result);
                }
            }
            else
            {
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
                        result += new System.Numerics.Vector<double>(tempVector) * coefficients[i][vectorIndex];
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
                        ReadOnlySpan<double> paddedSamplesSpan = new(paddedSamples);
                        ReadOnlySpan<System.Numerics.Vector<double>> coefSpan = new(coefficients[sidePoints]);

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
                        result += new System.Numerics.Vector<double>(tempVector) * coefficients[sidePoints + 1 + i][vectorIndex];
                    }
                    output[length - sidePoints + i] = System.Numerics.Vector.Sum(result);
                }
            }

            return output;
        }

        private Vector512<T> ConvertArrayToVector512<T>(ref T[] array, nuint index = 0)
            where T : struct
        {
            return Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(new ReadOnlySpan<T>(array)), index);
        }
        private Vector512<T> ConvertReadOnlySpanToVector512<T>(ReadOnlySpan<T> array, nuint index = 0)
            where T : struct
        {
            return Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(array), index);
        }
    }
}

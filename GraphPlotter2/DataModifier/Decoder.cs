using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DataModifier
{
    internal partial class DataModifier
    {

        /// <summary>
        /// Function to read CSV files and return time and thrust data.
        /// </summary>
        /// <param name="filePath">csv file path</param>
        /// <param name="timePrefix">timePrefix(eg, ms = 0.001). default is 0.001(millisecond)</param>
        /// <param name="linearEqCoefA">The value of A among the coefficients of the linear equation (Ax+B) that converts from voltage values to thrust. Default is 0.36394252776313896</param>
        /// <param name="linearEqCoefB">The value of B among the coefficients of the linear equation (Ax+B) that converts from voltage values to thrust. Default is -84.211384769940082</param>
        /// <returns></returns>
        private static (List<(double, double)>, Exception?) DecodeCSV(string filePath, double timePrefix = 0.001, double linearEqCoefA = 0.36394252776313896, double linearEqCoefB = -84.211384769940082)
        {
            List<(double, double)> data = new();
            Exception? exception = null;

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                data = lines
                    .AsParallel()
                    .Select(line =>
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 2 && double.TryParse(parts[0], out double time) && double.TryParse(parts[1], out double dataValue))
                        {
                            return (time * timePrefix, dataValue * linearEqCoefA + linearEqCoefB);
                        }
                        return (double.MinValue, double.MinValue);// 変換失敗時にはdouble型の最小値を返す
                    })
                    .Where(tuple => tuple.Item1 != double.MinValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            return (data, exception);
        }

        /// <summary>
        /// 時間データと、推力データの生データがこの順で記されたバイナリデータをdouble型の秒で記された時間データとdouble型の単位がニュートンである推力データに整形する関数。
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static (List<(double Time, double Data)>, Exception?) DecodeBinary(string filePath, double timePrefix = 0.000001, double linearEqCoefA = 1.0, double linearEqCoefB = 0.0)
        {
            List<(double Time, double Data)> dataList = new();
            Exception? exception = null;

            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Parallel.ForEach(
                    fileData.Buffer(8),
                    () => new List<(double Time, double Data)>(),
                    (chunk, state, localData) =>
                    {
                        for (int i = 0; i < chunk.Length; i += 8)
                        {
                            double time = BitConverter.ToInt32(chunk, i) * timePrefix;
                            double data = BitConverter.ToInt32(chunk, i + 4) * linearEqCoefA + linearEqCoefB;
                            localData.Add((time, data));
                        }
                        return localData;
                    },
                    localData =>
                    {
                        lock (dataList)
                        {
                            dataList.AddRange(localData);
                        }
                    });
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            return (dataList, exception);
        }
    }

    public static class Extensions
    {
        // バイナリデータを指定されたバッファサイズで分割する拡張メソッド
        public static IEnumerable<byte[]> Buffer(this byte[] source, int bufferSize)
        {
            for (int i = 0; i < source.Length; i += bufferSize)
            {
                yield return source.Skip(i).Take(bufferSize).ToArray();
            }
        }
    }
}

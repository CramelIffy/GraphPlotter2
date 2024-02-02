using System.Buffers.Binary;
using System.IO;

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
        private static (List<(double Time, double Data)>, Exception?, bool isSomeDataCannotRead) DecodeCSV(string filePath, double timePrefix, double linearEqCoefA, double linearEqCoefB)
        {
            List<(double, double)> data = new();
            Exception? exception = null;
            bool isSomeDataCannotRead = false;

            try
            {
                data = File.ReadAllLines(filePath)
                    .AsParallel()
                    .Select(line =>
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 2 && double.TryParse(parts[0], out double time) && double.TryParse(parts[1], out double dataValue))
                        {
                            return (time * timePrefix, dataValue * linearEqCoefA + linearEqCoefB);
                        }
                        else
                        {
                            isSomeDataCannotRead = true;
                            return (double.MinValue, double.MinValue);// 変換失敗時にはdouble型の最小値を返す
                        }
                    })
                    .Where(tuple => tuple.Item1 != double.MinValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                exception = ex;
                isSomeDataCannotRead = true;
            }

            return (data, exception, isSomeDataCannotRead);
        }

        /// <summary>
        /// 時間データと、推力データの生データがこの順で記されたバイナリデータをdouble型の秒で記された時間データとdouble型の単位がニュートンである推力データに整形する関数。
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static (List<(double Time, double Data)>, Exception?, bool isSomeDataCannotRead) DecodeBinary(string filePath, double timePrefix, double linearEqCoefA, double linearEqCoefB)
        {
            List<(double Time, double Data)> dataList = new();
            Exception? exception = null;
            bool isSomeDataCannotRead = false;

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
                            Int32 rData = BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(i * 8, 4)) & 0x00FFFFFF;
                            if ((rData & 0x00800000) != 0)
                            {
                                rData >>>= -8;
                                rData >>= 8;
                            }
                            UInt64 rTime = BinaryPrimitives.ReadUInt64LittleEndian(chunk.AsSpan(i * 8, 8)) >> 24;
                            double time = rTime * timePrefix;
                            double data = rData * linearEqCoefA + linearEqCoefB;
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
                isSomeDataCannotRead = true;
            }

            return (dataList, exception, isSomeDataCannotRead);
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

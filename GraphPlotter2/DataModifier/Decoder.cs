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
        /// マップ関数。電圧データを推力に変換する時用。
        /// y = ax + bの形で変形。
        /// </summary>
        /// <param name="data"></param>
        internal static void SRegressionLine(ref List<double> data, double a, double b)
        {
            data = data.Select(i => i * a + b).ToList();
        }

        /// <summary>
        /// CSVファイルを読み込んで、時間と推力データを返すプログラム。エラーとして返される可能性のある内容は以下の通り
        /// CANT_OPEN_FILE
        /// FILE_IS_TOO_LARGE
        /// COLUMN_DOES_NOT_EXIST
        /// DATA_IS_NOT_READABLE_AS_NUMERICAL_VALUES
        /// NO_DATA_EXISTS
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="timeColumnNum"></param>
        /// <param name="thrustColumnNum"></param>
        /// <returns></returns>
        private static (double[], double[], string) DecodeCSV(string filePath, int timeColumnNum, int thrustColumnNum, double timePrefix = 0.001)
        {
            var rawData = new List<string>();
            var timeList = new List<double>();
            var dataList = new List<double>();
            string error = "";

            try
            {
                using StreamReader reader = new(filePath);
                rawData = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            catch (IOException)
            {
                return (timeList.ToArray(), dataList.ToArray(), "CANT_OPEN_FILE");
            }
            catch (OutOfMemoryException)
            {
                return (timeList.ToArray(), dataList.ToArray(), "FILE_IS_TOO_LARGE");
            }

            int length = rawData.Count;

            string errorTemp = error;

            var locker = new object();

            Parallel.Invoke(() =>
            {
                timeList = new List<double>(
                    ParallelAssist.ParallelAssist.ForMulti(rawData, 0, length - 1, (x, i) =>
                    {
                        string[] values = x.Split(',');

                        if (timeColumnNum > values.Length - 1)
                        {
                            lock (locker)
                                if (errorTemp != "COLUMN_DOES_NOT_EXIST") errorTemp = "COLUMN_DOES_NOT_EXIST";
                            return double.MinValue;
                        }
                        else
                        {
                            if (double.TryParse(values[timeColumnNum].Trim(), out double temp))
                            {
                                return temp * timePrefix;
                            }
                            else if (errorTemp != "DATA_IS_NOT_READABLE_AS_NUMERICAL_VALUES" && i != 0)
                                lock (locker) errorTemp = "DATA_IS_NOT_READABLE_AS_NUMERICAL_VALUES";
                            return double.MinValue;
                        }
                    })
                );
            }, () =>
            {
                dataList = new List<double>(
                    ParallelAssist.ParallelAssist.ForMulti(rawData, 0, length - 1, (x, i) =>
                    {
                        string[] values = x.Split(',');

                        if (thrustColumnNum > values.Length - 1)
                        {
                            lock (locker)
                                if (errorTemp != "COLUMN_DOES_NOT_EXIST") errorTemp = "COLUMN_DOES_NOT_EXIST";
                            return double.MinValue;
                        }
                        else
                        {
                            if (double.TryParse(values[thrustColumnNum].Trim(), out double temp))
                            {
                                return temp;
                            }
                            else if (errorTemp != "DATA_IS_NOT_READABLE_AS_NUMERICAL_VALUES" && i != 0)
                                lock (locker) errorTemp = "DATA_IS_NOT_READABLE_AS_NUMERICAL_VALUES";
                            return double.MinValue;
                        }
                    })
                );
            });

            int howManyDelete = 0;
            for (int i = 0; i < length - howManyDelete; i++)
            {
                if (timeList[i] == double.MinValue || dataList[i] == double.MinValue)
                {
                    timeList.RemoveAt(i - howManyDelete);
                    dataList.RemoveAt(i - howManyDelete);
                    howManyDelete++;
                    i--;
                }
            }

            error = errorTemp;

            if (timeList.Count < 1) error = "NO_DATA_EXISTS";

            SRegressionLine(ref dataList, 0.36394252776313896, -84.211384769940082);

            return (timeList.ToArray(), dataList.ToArray(), error);
        }

        /// <summary>
        /// 時間データと、推力データの生データがこの順で記されたバイナリデータをdouble型の秒で記された時間データとdouble型の単位がニュートンである推力データに整形する関数。エラーとして返される可能性のある内容は以下の通り
        /// CANT_OPEN_FILE
        /// FILE_MAY_BE_CORRUPTED
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        private static (double[], double[], string) DecodeBinary(string filePath, double timePrefix = 0.001)
        {
            const int binDataByteSize = 4;//32bit(=4byte)のデータであることの指定

            FileInfo file = new FileInfo(filePath);
            try
            {
                long fileLength = file.Length;
                if (fileLength % binDataByteSize != 0 && MessageBox.Show("データが破損している恐れがあります。強制的に読み込みますか？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return (Array.Empty<double>(), Array.Empty<double>(), "FILE_MAY_BE_CORRUPTED");

                fileLength /= binDataByteSize;    //32bit(=4byte)あたりに変換

                double[] time = new double[fileLength / 2];
                double[] thrust = new double[fileLength / 2];
                int timeRaw = 0;
                int thrustRaw = 0;

                uint counter = 0;

                using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
                {
                    byte[] buf = new byte[binDataByteSize];
                    bool isNegative;

                    int bufIndex = 0;

                    //後半の論理式は時間データのみが存在し、推力データが存在しない時の対処
                    while (reader.Read(buf, 0, binDataByteSize) == binDataByteSize && counter < (fileLength / 2) * 2)
                    {
                        isNegative = buf[0] >> 7 == 1;

                        if (counter % 2 == 0)
                        {
                            for (bufIndex = 0; bufIndex < binDataByteSize; bufIndex++)
                            {
                                timeRaw <<= 8;
                                timeRaw |= buf[bufIndex];
                            }
                            if (isNegative) timeRaw |= int.MinValue;
                            time[counter / 2] = timeRaw * timePrefix;
                        }
                        else
                        {
                            for (bufIndex = 0; bufIndex < binDataByteSize; bufIndex++)
                            {
                                thrustRaw <<= 8;
                                thrustRaw |= buf[bufIndex];
                            }
                            if (isNegative) thrustRaw |= int.MinValue;
                            thrust[counter / 2] = ((double)(thrustRaw * 16) / 0x7FFFFF) * 196.1;//生データからmvへ換算し、その後ロードセルの定格容量/(定格出力*印加電圧)(=196.1)の積を取る
                        }

                        counter++;
                    }
                }
                return (time, thrust, "");
            }
            catch(IOException)
            {
                return (Array.Empty<double>(), Array.Empty<double>(), "CANT_OPEN_FILE");
            }
        }
    }
}

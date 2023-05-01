using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DataModifier
{
    class DataSet
	{
		internal double[] time;			//us
		internal double[] thrust;		//N
		internal double[] denoisedThrust;//N
		internal long ignitionIndex;	//
		internal long burnoutIndex;		//
		internal int burnTime;			//us
		internal double maxThrust;		//N
		internal double avgThrust;		//N
		internal double isp;			//N･s

		public DataSet() 
		{
			time = Array.Empty<double>();
			thrust = Array.Empty<double>();
			denoisedThrust = Array.Empty<double>();
			ignitionIndex = 0;
			burnoutIndex = 1;
			burnTime = 0;
			maxThrust = 0;
			avgThrust = 0;
			isp = 0;
		}

		public (double[], double[], double[], long, long, int, double, double, double) ReturnAllData()
		{
			return (time, thrust, denoisedThrust, ignitionIndex, burnoutIndex, burnTime, maxThrust, avgThrust, isp);
		}
    }

	/// <summary>
	/// ロケットの推力データを受け取り、加工する。
	/// </summary>
	internal partial class DataModifier
	{
		private DataSet _main;
		private DataSet _sub;
		public DataModifier() 
		{
			_main = new DataSet();
			_sub = new DataSet();
		}

        public void SetDatas(string filePath, bool isMain)
        {
        }

        public (double[], double[], double[], long, long, int, double, double, double) GetDatas(bool isMain)
		{
			return isMain ? _main.ReturnAllData() : _sub.ReturnAllData();
        }
	}
}

﻿using System.IO;
using System.Text.Json;

namespace Config
{
    public class SettingDatas
    {
        private int Trim(int num, int lowerBound, int upperBound)
        {
            if (num > upperBound)
                num = upperBound;
            else if (num < lowerBound)
                num = lowerBound;
            return num;
        }

        public bool MainGraph { get; set; }

        public bool SubGraph { get; set; }

        public bool BurningTime { get; set; }

        public bool MaxThrust { get; set; }

        public bool AverageThrust { get; set; }

        public bool TotalImpulse { get; set; }

        public string MainGraphName { get; set; }

        public string SubGraphName { get; set; }

        private int _subgraphopacity;
        public int SubGraphOpacity { get => _subgraphopacity; set => _subgraphopacity = Trim(value, 0, 100); }

        private int _undenoisedgraphgpacity;
        public int UndenoisedGraphOpacity { get => _undenoisedgraphgpacity; set => _undenoisedgraphgpacity = Trim(value, 0, 100); }

        private int _burningtimeopacity;
        public int BurningTimeOpacity { get => _burningtimeopacity; set => _burningtimeopacity = Trim(value, 0, 100); }

        private int _ignitiondetectionthreshold;
        public int IgnitionDetectionThreshold { get => _ignitiondetectionthreshold; set => _ignitiondetectionthreshold = Trim(value, 0, 100); }

        private int _burnoutdetectionthreshold;
        public int BurnoutDetectionThreshold { get => _burnoutdetectionthreshold; set => _burnoutdetectionthreshold = Trim(value, 0, 100); }

        public double PrefixOfTimeCSV { get; set; }

        public double PrefixOfTimeBIN { get; set; }

        public bool AverageDuplicateTimestamps { get; set; }

        public double SlopeCSV { get; set; }

        public double InterceptCSV { get; set; }

        public double SlopeBIN { get; set; }

        public double InterceptBIN { get; set; }

        public SettingDatas()
        {
            MainGraph = true;
            SubGraph = true;
            BurningTime = true;
            MaxThrust = true;
            AverageThrust = true;
            TotalImpulse = true;
            MainGraphName = "MainGraph";
            SubGraphName = "SubGraph";
            _subgraphopacity = 85;
            _undenoisedgraphgpacity = 20;
            _burningtimeopacity = 20;
            _ignitiondetectionthreshold = 5;
            _burnoutdetectionthreshold = 5;
            PrefixOfTimeCSV = 1E-03;
            PrefixOfTimeBIN = 4E-09;
            AverageDuplicateTimestamps = true;
            SlopeCSV = 0.36394252776313896;
            InterceptCSV = -84.211384769940082;
            SlopeBIN = 0.000290758;
            InterceptBIN = 0.0;
        }
    }

    public class SettingIO
    {
        private static string settingDirectoryPath = Directory.GetCurrentDirectory() + "\\Config\\";
        private static string settingFileName = "settings.config";
        public SettingDatas Data { get; set; }
        public void LoadConfig()
        {
            using var stream = new FileStream(settingDirectoryPath + settingFileName, FileMode.Open, FileAccess.Read);
            this.Data = JsonSerializer.DeserializeAsync<SettingDatas>(stream).Result ?? this.Data;
        }

        public async void WriteConfig()
        {
            if (!Directory.Exists(settingDirectoryPath))
                Directory.CreateDirectory(settingDirectoryPath);
            using var stream = new FileStream(settingDirectoryPath + settingFileName, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, Data);
        }

        public bool IsConfigFileExist()
        {
            return File.Exists(settingDirectoryPath + settingFileName);
        }

        public void InitConfig()
        {
            this.Data = new();
        }

        public SettingIO()
        {
            this.Data = new();
        }
    }
}

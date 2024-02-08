﻿using System.Windows;

namespace GraphPlotter2
{
    /// <summary>
    /// ProgressBar.xaml の相互作用ロジック
    /// </summary>
    public partial class ProgressBar : Window
    {
        public ProgressBar(uint maxProcNum = 100)
        {
            InitializeComponent();
            this.maxProcNum = maxProcNum;
        }

        public void UpdateProgress(double progress)
        {
            if (progressBar.Dispatcher.CheckAccess())
            {
                progressNum = progress;
                progressBar.Value = progressNum * 100 / maxProcNum;
            }
            else
                progressBar.Dispatcher.Invoke(() => UpdateProgress(progress));
        }

        public void IncreaseProgress(uint deltaProgress = 1)
        {
            if (progressBar.Dispatcher.CheckAccess())
            {
                progressNum++;
                progressBar.Value = progressNum * 100 / maxProcNum;
            }
            else
                progressBar.Dispatcher.Invoke(() => IncreaseProgress(deltaProgress));
        }

        public void UpdateStatus(string text)
        {
            if (processingDetail.Dispatcher.CheckAccess())
                processingDetail.Content = text;
            else
                processingDetail.Dispatcher.Invoke(() => UpdateStatus(text));
        }

        private uint maxProcNum;
        private double progressNum;
    }
}
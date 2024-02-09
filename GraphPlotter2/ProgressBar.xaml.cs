using System.Windows;

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
                progressCounter.Content = progressBar.Value.ToString("F") + "/100";
            }
            else
                progressBar.Dispatcher.Invoke(() => UpdateProgress(progress));
        }

        public void IncreaseProgress(double deltaProgress = 1.0)
        {
            if (progressBar.Dispatcher.CheckAccess())
            {
                progressNum += deltaProgress;
                progressBar.Value = progressNum * 100 / maxProcNum;
                progressCounter.Content = progressBar.Value.ToString("F") + "/100";
            }
            else
                progressBar.Dispatcher.Invoke(() => IncreaseProgress(deltaProgress));
        }

        public void UpdatePercentProgress(UInt64 microProgress)
        {
            if (progressBar.Dispatcher.CheckAccess())
            {
                progressBar.Value = progressNum * 100 / maxProcNum + microProgress * 0.01;
                progressCounter.Content = progressBar.Value.ToString("F") + "/100";
            }
            else
                progressBar.Dispatcher.Invoke(() => IncreaseProgress(microProgress));
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

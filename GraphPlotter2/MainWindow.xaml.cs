using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace GraphPlotter2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _allowDirectNavigation = false;
        private NavigatingCancelEventArgs? _navArgs = null;
        public MainWindow()
        {
            InitializeComponent();

            Uri defaultUri = new("/MainPage.xaml", UriKind.Relative);
            screen.Source = defaultUri;
        }

        /// <summary>
        /// 画面遷移(Setting画面へ)
        /// </summary>
        private void Frame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (screen.Content != null && !_allowDirectNavigation)
            {
                e.Cancel = true;
                _navArgs = e;

                // 遷移前のページを画像に変換しイメージに設定
                var visual = screen;
                var bounds = VisualTreeHelper.GetDescendantBounds(visual);
                var bitmap = new RenderTargetBitmap(
                    (int)bounds.Width,
                    (int)bounds.Height,
                    96.0,
                    96.0,
                    PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var vb = new VisualBrush(visual);
                    dc.DrawRectangle(vb, null, bounds);
                }
                bitmap.Render(dv);
                bitmap.Freeze();
                bufImage.Source = bitmap;

                // フレームに遷移先のページを設定
                _allowDirectNavigation = true;
                screen.Navigate(_navArgs.Content);

                // フレームを右からスライドさせるアニメーション
                ThicknessAnimation animation0 = new()
                {
                    From = new Thickness(screen.ActualWidth, 0, -1 * screen.ActualWidth, 0),
                    To = new Thickness(0, 0, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                screen.BeginAnimation(MarginProperty, animation0);

                // 遷移前ページを画像可した要素を左にスライドするアニメーション
                ThicknessAnimation animation1 = new()
                {
                    From = new Thickness(0, 0, 0, 0),
                    To = new Thickness(-1 * screen.ActualWidth, 0, screen.ActualWidth, 0),
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                bufImage.BeginAnimation(MarginProperty, animation1);
            }

            _allowDirectNavigation = false;
        }
    }
}

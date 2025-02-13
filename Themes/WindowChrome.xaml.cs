using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveSync.WPF.Views
{
    public partial class WindowChrome
    {
        public WindowChrome()
        {
            InitializeComponent();
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DependencyObject dependencyObject)
            {
                var window = Window.GetWindow(dependencyObject);
                if (window != null)
                {
                    if (e.ClickCount == 2)
                    {
                        window.WindowState = window.WindowState == WindowState.Maximized
                            ? WindowState.Normal
                            : WindowState.Maximized;
                    }
                    else
                    {
                        if (window.WindowState == WindowState.Maximized)
                        {
                            var point = e.GetPosition(window);
                            var width = window.RestoreBounds.Width;
                            var left = point.X - (width * 0.5);

                            window.WindowState = WindowState.Normal;
                            window.Left = left;
                            window.Top = 0;
                        }

                        window.DragMove();
                    }
                }
            }
        }

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject dependencyObject)
            {
                var window = Window.GetWindow(dependencyObject);
                if (window != null)
                {
                    window.WindowState = WindowState.Minimized;
                }
            }
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject dependencyObject)
            {
                var window = Window.GetWindow(dependencyObject);
                if (window != null)
                {
                    window.WindowState = window.WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                }
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject dependencyObject)
            {
                var window = Window.GetWindow(dependencyObject);
                if (window != null)
                {
                    window.Close();
                }
            }
        }
    }
}
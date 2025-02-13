using System.Windows;
using System.Windows.Input;

namespace DriveSync.WPF.Views
{
    public class ModernWindowBase : Window
    {
        public static RoutedCommand DragMoveCommand { get; } = new RoutedCommand();
        public static RoutedCommand MinimizeCommand { get; } = new RoutedCommand();
        public static RoutedCommand MaximizeRestoreCommand { get; } = new RoutedCommand();
        public static RoutedCommand CloseCommand { get; } = new RoutedCommand();

        public ModernWindowBase()
        {
            // Set the window style when creating new instance
            Style = (Style)FindResource("ModernWindowStyle");

            CommandBindings.Add(new CommandBinding(DragMoveCommand, OnDragMove));
            CommandBindings.Add(new CommandBinding(MinimizeCommand, OnMinimizeButtonClick));
            CommandBindings.Add(new CommandBinding(MaximizeRestoreCommand, OnMaximizeRestoreButtonClick));
            CommandBindings.Add(new CommandBinding(CloseCommand, OnCloseButtonClick));
        }

        private void OnDragMove(object sender, ExecutedRoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                // Calculate restore position when dragging a maximized window
                var point = PointToScreen(Mouse.GetPosition(this));
                var width = RestoreBounds.Width;
                var height = RestoreBounds.Height;
                var left = point.X - (width * 0.5);
                var top = point.Y;
                WindowState = WindowState.Normal;
                Left = left;
                Top = top;
            }
            DragMove();
        }

        private void OnMinimizeButtonClick(object sender, ExecutedRoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void OnCloseButtonClick(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
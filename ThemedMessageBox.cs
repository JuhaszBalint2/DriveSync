using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DriveSync.WPF.Views
{
    public static class ThemedMessageBox
    {
        private static Button CreateButton(string content, Action clickAction, string theme)
        {
            var button = new Button
            {
                Content = content,
                Width = 100,
                Height = 36,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5, 0, 0, 0),
                Background = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(64, 64, 64))
                    : new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                Foreground = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? Brushes.White
                    : Brushes.Black,
                BorderThickness = new Thickness(1),
                BorderBrush = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(97, 97, 97))
                    : new SolidColorBrush(Color.FromRgb(173, 173, 173))
            };

            button.Click += (s, e) => clickAction();
            return button;
        }

        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var settings = AppSettings.Load();
            string effectiveTheme = settings.GetEffectiveTheme();

            var messageWindow = new Window
            {
                Width = 400,
                Height = 200,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var mainBorder = new Border
            {
                Background = effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
                    : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(97, 97, 97))
                    : new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.3,
                    BlurRadius = 15,
                    ShadowDepth = 2
                }
            };

            var contentGrid = new Grid { Margin = new Thickness(20) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength() });  // Using default constructor for Auto

            // Create a grid for the icon and message to be properly positioned
            var messageGrid = new Grid();
            messageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength() });  // Using default constructor for Auto
            messageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(messageGrid, 0);

            var iconControl = new Image
            {
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)  // Add more right margin for better spacing
            };

            switch (icon)
            {
                case MessageBoxImage.Warning:
                    iconControl.Source = CreateWarningIcon(effectiveTheme);
                    break;
                case MessageBoxImage.Error:
                    iconControl.Source = CreateErrorIcon(effectiveTheme);
                    break;
                case MessageBoxImage.Information:
                    iconControl.Source = CreateInfoIcon(effectiveTheme);
                    break;
                default:
                    iconControl.Visibility = Visibility.Collapsed;
                    break;
            }

            // Add icon to first column if it's visible
            if (icon != MessageBoxImage.None)
            {
                Grid.SetColumn(iconControl, 0);
                messageGrid.Children.Add(iconControl);
            }

            var messageTextBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };

            // Set message to second column
            Grid.SetColumn(messageTextBlock, icon != MessageBoxImage.None ? 1 : 0);
            messageGrid.Children.Add(messageTextBlock);

            // Add message grid to content grid
            contentGrid.Children.Add(messageGrid);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 15, 0, 0)  // Add top margin for spacing from message
            };
            Grid.SetRow(buttonPanel, 1);

            MessageBoxResult result = MessageBoxResult.None;

            switch (button)
            {
                case MessageBoxButton.OK:
                    buttonPanel.Children.Add(CreateButton("OK", () =>
                    {
                        result = MessageBoxResult.OK;
                        messageWindow.Close();
                    }, effectiveTheme));
                    break;
                case MessageBoxButton.OKCancel:
                    buttonPanel.Children.Add(CreateButton("OK", () =>
                    {
                        result = MessageBoxResult.OK;
                        messageWindow.Close();
                    }, effectiveTheme));
                    buttonPanel.Children.Add(CreateButton("Cancel", () =>
                    {
                        result = MessageBoxResult.Cancel;
                        messageWindow.Close();
                    }, effectiveTheme));
                    break;
                case MessageBoxButton.YesNo:
                    buttonPanel.Children.Add(CreateButton("Yes", () =>
                    {
                        result = MessageBoxResult.Yes;
                        messageWindow.Close();
                    }, effectiveTheme));
                    buttonPanel.Children.Add(CreateButton("No", () =>
                    {
                        result = MessageBoxResult.No;
                        messageWindow.Close();
                    }, effectiveTheme));
                    break;
                case MessageBoxButton.YesNoCancel:
                    buttonPanel.Children.Add(CreateButton("Yes", () =>
                    {
                        result = MessageBoxResult.Yes;
                        messageWindow.Close();
                    }, effectiveTheme));
                    buttonPanel.Children.Add(CreateButton("No", () =>
                    {
                        result = MessageBoxResult.No;
                        messageWindow.Close();
                    }, effectiveTheme));
                    buttonPanel.Children.Add(CreateButton("Cancel", () =>
                    {
                        result = MessageBoxResult.Cancel;
                        messageWindow.Close();
                    }, effectiveTheme));
                    break;
            }

            contentGrid.Children.Add(buttonPanel);
            mainBorder.Child = contentGrid;
            messageWindow.Content = mainBorder;

            messageWindow.ShowDialog();

            return result;
        }

        private static BitmapSource CreateWarningIcon(string theme)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                PathGeometry triangleGeometry = new PathGeometry();
                PathFigure pathFigure = new PathFigure
                {
                    StartPoint = new Point(16, 0),
                    IsClosed = true
                };

                pathFigure.Segments.Add(new LineSegment(new Point(32, 28), true));
                pathFigure.Segments.Add(new LineSegment(new Point(0, 28), true));

                triangleGeometry.Figures.Add(pathFigure);

                SolidColorBrush fillBrush = new SolidColorBrush(Color.FromRgb(255, 204, 0));

                drawingContext.DrawGeometry(fillBrush, new Pen(Brushes.Black, 2), triangleGeometry);

                FormattedText exclamationText = new FormattedText(
                    "!",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    24,
                    Brushes.Black
                );

                Point textPosition = new Point(
                    (32 - exclamationText.Width) / 2,
                    (28 - exclamationText.Height) / 2 - 2
                );

                drawingContext.DrawText(exclamationText, textPosition);
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            return bitmap;
        }

        private static BitmapSource CreateErrorIcon(string theme)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                Pen pen = new Pen(Brushes.White, 3);
                SolidColorBrush fillBrush = new SolidColorBrush(Color.FromRgb(232, 17, 35));

                drawingContext.DrawLine(pen, new Point(0, 0), new Point(32, 32));
                drawingContext.DrawLine(pen, new Point(32, 0), new Point(0, 32));
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            return bitmap;
        }

        private static BitmapSource CreateInfoIcon(string theme)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                Pen pen = new Pen(Brushes.White, 2);
                SolidColorBrush fillBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));

                drawingContext.DrawEllipse(fillBrush, pen, new Point(16, 16), 16, 16);

                FormattedText text = new FormattedText(
                    "i",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    20,
                    Brushes.White
                );
                drawingContext.DrawText(text, new Point(12, 4));
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            return bitmap;
        }
    }
}
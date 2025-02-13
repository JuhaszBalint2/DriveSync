using System;
using System.Windows;
using System.Windows.Media;

namespace DriveSync.WPF.Helpers
{
    public static class DpiHelper
    {
        private const double DefaultDpi = 96.0;

        public static double GetScalingFactor(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0;
        }

        public static double ScaleValue(double value, Visual visual)
        {
            var scalingFactor = GetScalingFactor(visual);
            return value * (DefaultDpi / (DefaultDpi * scalingFactor));
        }

        public static Thickness ScaleThickness(Thickness thickness, Visual visual)
        {
            var scalingFactor = GetScalingFactor(visual);
            var scale = DefaultDpi / (DefaultDpi * scalingFactor);
            return new Thickness(
                thickness.Left * scale,
                thickness.Top * scale,
                thickness.Right * scale,
                thickness.Bottom * scale);
        }

        public static double GetDpiScale(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
            {
                return DefaultDpi * source.CompositionTarget.TransformToDevice.M11 / DefaultDpi;
            }
            return 1.0;
        }
    }
}
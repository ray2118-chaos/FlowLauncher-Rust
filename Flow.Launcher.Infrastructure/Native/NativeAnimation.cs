using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Flow.Launcher.Infrastructure.Native
{
    public enum NativeAnimationCurve : uint
    {
        EaseInOutCubic = 0,
        EaseOutQuint = 1,
        EaseOutExpo = 2,
        SpringFluid = 3,
        FluidGlassRefract = 4,
        FastOutSlowIn = 5
    }

    /// <summary>
    /// Modular WPF easing function powered directly by the high-performance Rust physics engine.
    /// </summary>
    public class NativeSpringEase : EasingFunctionBase
    {
        public static readonly DependencyProperty CurveProperty =
            DependencyProperty.Register(
                nameof(Curve),
                typeof(NativeAnimationCurve),
                typeof(NativeSpringEase),
                new PropertyMetadata(NativeAnimationCurve.SpringFluid));

        public NativeAnimationCurve Curve
        {
            get => (NativeAnimationCurve)GetValue(CurveProperty);
            set => SetValue(CurveProperty, value);
        }

        public NativeSpringEase() { }

        public NativeSpringEase(NativeAnimationCurve curve)
        {
            Curve = curve;
        }

        protected override Freezable CreateInstanceCore() => new NativeSpringEase();

        protected override double EaseInCore(double normalizedTime)
        {
            return FlowNativeMethods.EvaluateAnimationCurve((uint)Curve, normalizedTime);
        }
    }

    /// <summary>
    /// Native animation controller providing buttery smooth physics-based animations.
    /// </summary>
    public static class NativeAnimation
    {
        public static DoubleAnimation CreateDoubleAnimation(
            double from,
            double to,
            TimeSpan duration,
            NativeAnimationCurve curve = NativeAnimationCurve.SpringFluid)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = new NativeSpringEase(curve),
                FillBehavior = FillBehavior.HoldEnd
            };
        }

        public static ThicknessAnimation CreateThicknessAnimation(
            Thickness from,
            Thickness to,
            TimeSpan duration,
            NativeAnimationCurve curve = NativeAnimationCurve.SpringFluid)
        {
            return new ThicknessAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = new NativeSpringEase(curve),
                FillBehavior = FillBehavior.HoldEnd
            };
        }

        /// <summary>
        /// Generates an analytical physics-based keyframe animation directly computed by the Rust native engine at 120 FPS.
        /// </summary>
        public static DoubleAnimationUsingKeyFrames CreateSpringKeyframeAnimation(
            double from,
            double to,
            int durationMs,
            double stiffness = 180.0,
            double dampingRatio = 0.72)
        {
            var anim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(durationMs),
                FillBehavior = FillBehavior.HoldEnd
            };

            Span<double> keyframes = stackalloc double[256];
            if (FlowNativeMethods.GenerateSpringKeyframes(from, to, (uint)durationMs, 120, stiffness, dampingRatio, keyframes, out int count) && count > 1)
            {
                var stepTime = (double)durationMs / (count - 1);
                for (int i = 0; i < count; i++)
                {
                    var keyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * stepTime));
                    anim.KeyFrames.Add(new LinearDoubleKeyFrame(keyframes[i], keyTime));
                }
            }
            else
            {
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs))));
            }

            return anim;
        }
    }
}

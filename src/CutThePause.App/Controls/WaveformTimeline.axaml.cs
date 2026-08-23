using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CutThePause.Core.Models;
using CutThePause.Core.Services;

namespace CutThePause.App.Controls;

public sealed class WaveformTimeline : Control
{
    public static readonly StyledProperty<IReadOnlyList<float>> PeaksProperty =
        AvaloniaProperty.Register<WaveformTimeline, IReadOnlyList<float>>(nameof(Peaks), Array.Empty<float>());

    public static readonly StyledProperty<IReadOnlyList<CutCandidate>> CutsProperty =
        AvaloniaProperty.Register<WaveformTimeline, IReadOnlyList<CutCandidate>>(nameof(Cuts), Array.Empty<CutCandidate>());

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<WaveformTimeline, TimeSpan>(nameof(Duration), TimeSpan.Zero);

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#0B1220"));
    private static readonly IBrush WaveformBrush = new SolidColorBrush(Color.Parse("#14D1C8"));
    private static readonly IBrush EnabledCutBrush = new SolidColorBrush(Color.FromArgb(115, 255, 182, 86));
    private static readonly IBrush DisabledCutBrush = new SolidColorBrush(Color.FromArgb(100, 90, 155, 165));
    private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.FromArgb(90, 20, 209, 200));
    private static readonly Pen CenterLinePen = new(new SolidColorBrush(Color.FromArgb(100, 169, 182, 200)), 1);

    private bool _isDragging;
    private double _dragStartX;
    private double _dragCurrentX;

    public IReadOnlyList<float> Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public IReadOnlyList<CutCandidate> Cuts
    {
        get => GetValue(CutsProperty);
        set => SetValue(CutsProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public event EventHandler<TimelinePositionEventArgs>? CutClicked;

    public event EventHandler<TimelineRangeSelectedEventArgs>? RangeSelected;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PeaksProperty || change.Property == CutsProperty || change.Property == DurationProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        context.FillRectangle(TrackBrush, new Rect(0, 0, width, height));
        context.DrawLine(CenterLinePen, new Point(0, height / 2d), new Point(width, height / 2d));

        if (Duration > TimeSpan.Zero)
        {
            foreach (var cut in Cuts)
            {
                var start = TimeToX(cut.Start, width);
                var end = TimeToX(cut.End, width);
                context.FillRectangle(
                    cut.IsEnabled ? EnabledCutBrush : DisabledCutBrush,
                    new Rect(start, 4, Math.Max(1d, end - start), Math.Max(1d, height - 8)));
            }
        }

        if (Peaks.Count > 0)
        {
            var barWidth = Math.Max(1d, width / Peaks.Count);
            for (var index = 0; index < Peaks.Count; index++)
            {
                var amplitude = Math.Clamp(Peaks[index], 0f, 1f);
                var barHeight = Math.Max(2d, amplitude * (height - 16d));
                var x = index * barWidth;
                context.FillRectangle(
                    WaveformBrush,
                    new Rect(x, (height - barHeight) / 2d, Math.Max(1d, barWidth - 1d), barHeight));
            }
        }

        if (_isDragging)
        {
            var left = Math.Min(_dragStartX, _dragCurrentX);
            var right = Math.Max(_dragStartX, _dragCurrentX);
            context.FillRectangle(SelectionBrush, new Rect(left, 0, Math.Max(1d, right - left), height));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _dragStartX = e.GetPosition(this).X;
        _dragCurrentX = _dragStartX;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging)
        {
            return;
        }

        _dragCurrentX = e.GetPosition(this).X;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
        {
            return;
        }

        _dragCurrentX = e.GetPosition(this).X;
        _isDragging = false;
        e.Pointer.Capture(null);

        var range = TimelineSelection.RangeFromPixels(_dragStartX, _dragCurrentX, Bounds.Width, Duration);
        if (range is { } selectedRange)
        {
            RangeSelected?.Invoke(this, new TimelineRangeSelectedEventArgs(selectedRange));
        }
        else if (TimelineSelection.FindCutAtPixel(_dragCurrentX, Bounds.Width, Duration, Cuts) is not null)
        {
            CutClicked?.Invoke(
                this,
                new TimelinePositionEventArgs(TimelineSelection.TimeAt(_dragCurrentX, Bounds.Width, Duration)));
        }

        e.Handled = true;
        InvalidateVisual();
    }

    private double TimeToX(TimeSpan time, double width) =>
        Duration <= TimeSpan.Zero
            ? 0d
            : Math.Clamp(time.TotalSeconds / Duration.TotalSeconds, 0d, 1d) * width;
}

public sealed class TimelinePositionEventArgs : EventArgs
{
    public TimelinePositionEventArgs(TimeSpan position)
    {
        Position = position;
    }

    public TimeSpan Position { get; }
}

public sealed class TimelineRangeSelectedEventArgs : EventArgs
{
    public TimelineRangeSelectedEventArgs(TimeRange range)
    {
        Range = range;
    }

    public TimeRange Range { get; }
}

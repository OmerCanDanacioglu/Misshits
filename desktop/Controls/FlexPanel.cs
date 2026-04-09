using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Misshits.Desktop.Controls;

/// <summary>
/// Custom panel that distributes children proportionally by an attached Weight property.
/// Mimics CSS flex: N 1 0 behavior.
/// </summary>
public class FlexPanel : Panel
{
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<FlexPanel, Control, double>("Weight", 1.0);

    public static double GetWeight(Control element) => element.GetValue(WeightProperty);
    public static void SetWeight(Control element, double value) => element.SetValue(WeightProperty, value);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<FlexPanel, double>(nameof(Spacing), 2.0);

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        }
        return new Size(availableSize.Width, maxHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0) return finalSize;

        double totalWeight = 0;
        foreach (var child in Children)
            totalWeight += GetWeight(child);

        double totalSpacing = Spacing * (Children.Count - 1);
        double availableWidth = finalSize.Width - totalSpacing;

        double x = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            var weight = GetWeight(Children[i]);
            var width = availableWidth * weight / totalWeight;
            Children[i].Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }

        return finalSize;
    }
}

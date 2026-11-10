namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Defines the initial dimensions used when the host opens a plugin window.
/// These values are not persisted after the user resizes the window.
/// </summary>
public sealed record PluginWindowOptions
{
    /// <summary>
    /// Every value must be a finite number above 0, and the initial size must be at least the minimum size;
    /// otherwise <see cref="ArgumentOutOfRangeException"/> is thrown.
    /// </summary>
    /// <param name="width">The initial width.</param>
    /// <param name="height">The initial height.</param>
    /// <param name="minWidth">The minimum width.</param>
    /// <param name="minHeight">The minimum height.</param>
    public PluginWindowOptions(
        double width,
        double height,
        double minWidth,
        double minHeight)
    {
        ValidateDimension(width, nameof(width));
        ValidateDimension(height, nameof(height));
        ValidateDimension(minWidth, nameof(minWidth));
        ValidateDimension(minHeight, nameof(minHeight));

        if (width < minWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "The initial width must be greater than or equal to the minimum width.");
        }

        if (height < minHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "The initial height must be greater than or equal to the minimum height.");
        }

        Width = width;
        Height = height;
        MinWidth = minWidth;
        MinHeight = minHeight;
    }

    /// <summary>The initial width.</summary>
    public double Width { get; }

    /// <summary>The initial height.</summary>
    public double Height { get; }

    /// <summary>The minimum width.</summary>
    public double MinWidth { get; }

    /// <summary>The minimum height.</summary>
    public double MinHeight { get; }

    private static void ValidateDimension(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Window dimensions must be finite values greater than zero.");
        }
    }
}

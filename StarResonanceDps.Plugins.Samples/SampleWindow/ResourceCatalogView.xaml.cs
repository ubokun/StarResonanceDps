using System.Windows.Controls;
using System.Windows.Input;

namespace SampleWindow;

public partial class ResourceCatalogView : UserControl
{
    private int _clickCount;

    public ResourceCatalogView()
    {
        InitializeComponent();
    }

    private void ClickCountText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _clickCount++;
        ClickCountText.Text = $"ToolTipOwner.Default ({_clickCount})";
    }
}

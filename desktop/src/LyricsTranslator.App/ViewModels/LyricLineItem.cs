using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace LyricsTranslator.ViewModels;

public sealed partial class LyricLineItem : ObservableObject
{
    [ObservableProperty] private string _original = string.Empty;
    [ObservableProperty] private string _translation = string.Empty;
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private double _lineOpacity = 0.34;
    [ObservableProperty] private double _fontSize = 14;
    [ObservableProperty] private Thickness _accentThickness = new(0);

    public void ApplyWindow(int distance)
    {
        IsCurrent = distance == 0;
        LineOpacity = distance switch
        {
            0 => 1,
            1 => 0.82,
            2 => 0.52,
            _ => 0.28,
        };
        FontSize = distance == 0 ? 18 : 14;
        AccentThickness = distance == 0 ? new Thickness(3, 0, 0, 0) : new Thickness(0);
    }
}

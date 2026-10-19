using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ui.Wpf.Views;

public partial class NavItem : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavItem), new PropertyMetadata(""));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(NavItem));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NavItem), new PropertyMetadata(false));

    public static readonly DependencyProperty BadgeCountProperty =
        DependencyProperty.Register(nameof(BadgeCount), typeof(int), typeof(NavItem), new PropertyMetadata(0));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(NavItem));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(NavItem));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry IconData { get => (Geometry)GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public int BadgeCount { get => (int)GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }
    public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public NavItem() => InitializeComponent();
}

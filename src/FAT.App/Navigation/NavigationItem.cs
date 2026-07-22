using MaterialDesignThemes.Wpf;

namespace FAT.App.Navigation;

/// <summary>
/// One entry in the left navigation rail.
///
/// KEY MERGE-CONFLICT COUNTERMEASURE (see docs/TEAM.md):
/// The sidebar is BUILT from the NavigationItem instances that each module
/// registers in its own Startup/&lt;Module&gt;Registration.cs file. The menu is
/// never hard-coded in MainWindow.xaml.
///
/// That lets five people add screens without any of them editing the same XAML
/// file - historically the number-one source of conflicts in a group WPF project.
/// </summary>
/// <param name="Title">Label shown in the menu.</param>
/// <param name="Icon">Material Design icon.</param>
/// <param name="ViewModelType">View model to navigate to.</param>
/// <param name="Order">Position in the menu; lower numbers appear first.</param>
/// <param name="RequiresAdmin">When true, only Admin accounts see the entry.</param>
/// <param name="Group">Optional group heading, for example "Administration".</param>
public sealed record NavigationItem(
    string Title,
    PackIconKind Icon,
    Type ViewModelType,
    int Order,
    bool RequiresAdmin = false,
    string? Group = null);

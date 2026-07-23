using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.Domain.Constants;

namespace FAT.App.ViewModels.Common;

/// <summary>
/// Search, sort, paging and the empty/error states, written ONCE.
///
/// Every admin list screen needs the same six behaviours. Without a shared base
/// each of the six screens grows its own copy, and they drift: one paginates
/// from zero, another from one; one clears the error on reload, another does
/// not. Derived screens supply only <see cref="LoadItemsAsync"/>.
///
/// Paging happens in memory, on the already-filtered list. That is the right
/// trade here - the largest of these lists is 135 subjects, and pushing OFFSET
/// / FETCH into every service method would complicate all of them to save a
/// query that is already trivial.
/// </summary>
public abstract partial class PagedListViewModel<T> : ViewModelBase
{
    /// <summary>Everything that matched the current filter, before paging.</summary>
    private IReadOnlyList<T> _allItems = [];

    /// <summary>The current page - what the DataGrid binds to.</summary>
    [ObservableProperty]
    private ObservableCollection<T> _items = [];

    [ObservableProperty]
    private T? _selectedItem;

    [ObservableProperty]
    private string? _searchKeyword;

    [ObservableProperty]
    private string? _sortColumn;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private int _pageIndex;

    [ObservableProperty]
    private int _pageSize = CatalogRules.DefaultPageSize;

    [ObservableProperty]
    private int _totalCount;

    /// <summary>Transient success text, shown in a banner and cleared on the next action.</summary>
    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public int PageCount => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int PageNumber => PageIndex + 1;

    public bool CanGoPrevious => PageIndex > 0;

    public bool CanGoNext => PageIndex < PageCount - 1;

    /// <summary>True only when a completed load found nothing - never while loading.</summary>
    public bool IsEmpty => !IsBusy && TotalCount == 0 && !HasError;

    /// <summary>Loads the rows that match the current filter. Paging is applied for you.</summary>
    protected abstract Task<IReadOnlyList<T>> LoadItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Orders the rows. The base leaves them in the order the service returned,
    /// which is already the meaningful one for most screens.
    /// </summary>
    protected virtual IReadOnlyList<T> ApplySort(IReadOnlyList<T> items) => items;

    /// <summary>Reloads from the service and returns to the first page.</summary>
    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            _allItems = ApplySort(await LoadItemsAsync(cancellationToken));
            TotalCount = _allItems.Count;

            // Back to page one: after a filter change the old page index usually
            // points past the end of the new result set, which would show a
            // blank grid over a non-empty list.
            PageIndex = 0;
            UpdatePage();
        });

        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken = default)
        => await RefreshAsync(cancellationToken);

    [RelayCommand]
    private async Task ClearSearchAsync(CancellationToken cancellationToken = default)
    {
        SearchKeyword = null;
        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        PageIndex++;
        UpdatePage();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        PageIndex--;
        UpdatePage();
    }

    /// <summary>Sorts by a column; clicking the same column again reverses it.</summary>
    [RelayCommand]
    private void Sort(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return;
        }

        if (string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = false;
        }

        _allItems = ApplySort(_allItems);
        PageIndex = 0;
        UpdatePage();
    }

    /// <summary>Copies the current page into <see cref="Items"/>.</summary>
    protected void UpdatePage()
    {
        var page = _allItems.Skip(PageIndex * PageSize).Take(PageSize).ToList();

        Items.Clear();
        foreach (var item in page)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageNumber));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Runs an action, then reloads and reports success.
    ///
    /// Every create/update/delete on these screens ends the same way, and the
    /// step most easily forgotten is the reload - which leaves the grid showing
    /// data the database no longer holds.
    /// </summary>
    protected async Task RunAndRefreshAsync(Func<Task> action, string successMessage)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            await action();

            _allItems = ApplySort(await LoadItemsAsync(CancellationToken.None));
            TotalCount = _allItems.Count;

            // Deleting the last row of the final page would otherwise leave the
            // user on a page that no longer exists.
            if (PageIndex > PageCount - 1)
            {
                PageIndex = Math.Max(0, PageCount - 1);
            }

            UpdatePage();
            StatusMessage = successMessage;
        });

        OnPropertyChanged(nameof(IsEmpty));
    }
}

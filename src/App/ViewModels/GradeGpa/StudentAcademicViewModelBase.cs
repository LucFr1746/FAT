using App.Navigation;
using Services.Abstractions;

namespace App.ViewModels.GradeGpa;

/// <summary>Shared navigation lifecycle for student-only academic screens.</summary>
public abstract partial class StudentAcademicViewModelBase : ViewModelBase, INavigationAware
{
    private readonly ICurrentUserContext _currentUser;

    protected StudentAcademicViewModelBase(ICurrentUserContext currentUser)
        => _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    protected int StudentId => _currentUser.RequireStudentId();

    public Task OnNavigatedToAsync(
        object? parameter,
        CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken);

    protected abstract Task LoadAsync(CancellationToken cancellationToken);
}

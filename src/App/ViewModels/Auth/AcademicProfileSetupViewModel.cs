using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Mandatory First Login Academic Profile Setup screen.
/// Collects and validates Họ và tên, Email, Số điện thoại, Ngành học, and Lớp học.
/// </summary>
public partial class AcademicProfileSetupViewModel : ViewModelBase, INavigationAware
{
    private readonly IUserService _userService;
    private readonly ICourseService _courseService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _studentCode = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _classOptions = new();

    [ObservableProperty]
    private string? _selectedClassName;

    [ObservableProperty]
    private ObservableCollection<MajorDto> _majors = new();

    [ObservableProperty]
    private MajorDto? _selectedMajor;

    [ObservableProperty]
    private ObservableCollection<string> _termOptions = new();

    [ObservableProperty]
    private string _selectedTermOption = "Kỳ 1";

    [ObservableProperty]
    private ObservableCollection<int> _termNumbers = new();

    [ObservableProperty]
    private int _selectedTermNo = 1;

    public AcademicProfileSetupViewModel(
        IUserService userService,
        ICourseService courseService,
        ICurrentUserContext currentUserContext,
        INavigationService navigationService)
    {
        _userService = userService;
        _courseService = courseService;
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;
        Title = "Hoàn Tất Hồ Sơ Học Tập Ban Đầu - FAT";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            var user = _currentUserContext.User;
            if (user != null)
            {
                StudentCode = user.StudentCode ?? user.Username;
                if (!string.IsNullOrWhiteSpace(user.FullName) && user.FullName != user.Username)
                {
                    FullName = user.FullName;
                }

                if (user.StudentId is int sId)
                {
                    var existingProfile = await _userService.GetProfileAsync(sId, cancellationToken);
                    if (existingProfile != null)
                    {
                        if (!string.IsNullOrWhiteSpace(existingProfile.FullName))
                        {
                            FullName = existingProfile.FullName;
                        }

                        if (!string.IsNullOrWhiteSpace(existingProfile.Email))
                        {
                            Email = existingProfile.Email;
                        }

                        if (!string.IsNullOrWhiteSpace(existingProfile.Phone))
                        {
                            Phone = existingProfile.Phone;
                        }

                        if (!string.IsNullOrWhiteSpace(existingProfile.ClassName))
                        {
                            SelectedClassName = existingProfile.ClassName;
                        }

                        if (existingProfile.CurrentTermNo.HasValue && existingProfile.CurrentTermNo.Value >= 1 && existingProfile.CurrentTermNo.Value <= 9)
                        {
                            SelectedTermNo = existingProfile.CurrentTermNo.Value;
                        }
                    }
                }
            }

            var majors = await _courseService.GetMajorsAsync(cancellationToken);
            Majors.Clear();
            foreach (var major in majors)
            {
                Majors.Add(major);
            }

            SelectedMajor = Majors.FirstOrDefault();

            ClassOptions.Clear();
            var predefinedClasses = new[]
            {
                "BIT_SE_K19D_K20A",
                "BIT_SE_K20D_K21A",
                "BIT_AI_K19D-20A",
                "BIT_AI_K20D-21A",
                "BBA_IB_K19D20A",
                "BBA_IB_K20D21A",
                "BIT_SE_K19A",
                "BIT_SE_K19B",
                "BIT_SE_K20A"
            };

            foreach (var c in predefinedClasses)
            {
                ClassOptions.Add(c);
            }

            if (string.IsNullOrWhiteSpace(SelectedClassName))
            {
                SelectedClassName = ClassOptions.FirstOrDefault();
            }

            TermNumbers.Clear();
            TermOptions.Clear();
            for (int i = 1; i <= 9; i++)
            {
                TermNumbers.Add(i);
                TermOptions.Add($"Kỳ {i}");
            }

            SelectedTermOption = $"Kỳ {SelectedTermNo}";
            if (!TermOptions.Contains(SelectedTermOption))
            {
                SelectedTermOption = "Kỳ 1";
            }
        });
    }


    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "* Vui lòng nhập Họ và tên đầy đủ.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "* Vui lòng nhập địa chỉ Email.";
            return;
        }

        if (!Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            ErrorMessage = "* Định dạng Email không hợp lệ (VD: student@fpt.edu.vn).";
            return;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "* Vui lòng nhập Số điện thoại.";
            return;
        }

        if (!Regex.IsMatch(Phone.Trim(), @"^(0|\+84)[3|5|7|8|9][0-9]{8}$"))
        {
            ErrorMessage = "* Số điện thoại không hợp lệ (Định dạng SĐT Việt Nam 10 chữ số).";
            return;
        }

        if (SelectedMajor == null)
        {
            ErrorMessage = "* Vui lòng chọn Ngành học của bạn.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedClassName))
        {
            ErrorMessage = "* Vui lòng chọn Lớp học.";
            return;
        }

        if (_currentUserContext.StudentId is not int studentId)
        {
            ErrorMessage = "* Không tìm thấy thông tin tài khoản sinh viên.";
            return;
        }

        int termNo = 1;
        if (!string.IsNullOrWhiteSpace(SelectedTermOption) && SelectedTermOption.StartsWith("Kỳ "))
        {
            int.TryParse(SelectedTermOption.Replace("Kỳ ", "").Trim(), out termNo);
        }
        if (termNo < 1 || termNo > 9)
        {
            termNo = 1;
        }

        SelectedTermNo = termNo;

        await RunBusyAsync(async () =>
        {
            await _userService.CompleteAcademicProfileAsync(
                studentId: studentId,
                fullName: FullName.Trim(),
                email: Email.Trim().ToLowerInvariant(),
                phone: Phone.Trim(),
                majorId: SelectedMajor.MajorId,
                className: SelectedClassName.Trim(),
                currentTermNo: SelectedTermNo);

            // Update session context with completed profile flag
            var current = _currentUserContext.User;
            if (current != null)
            {
                _currentUserContext.SetUser(current with { IsProfileCompleted = true, FullName = FullName.Trim() });
            }


            // Proceed to main application dashboard
            await _navigationService.NavigateToAsync<DashboardViewModel>();
        });
    }
}

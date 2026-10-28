using HomeMenu.Core;

namespace HomeMenu.Wpf;

public sealed class AuthViewModel : ObservableObject
{
    private readonly IAuthService service;
    private readonly Action success;
    private bool isRegistration;
    private string error = "";
    public string Login { get; set; } = "";
    public string Name { get; set; } = "";
    public string Password { get; set; } = "";
    public string Repeat { get; set; } = "";
    public bool IsRegistration { get => isRegistration; private set { Set(ref isRegistration, value); Changed(nameof(ActionLabel)); Changed(nameof(ToggleLabel)); } }
    public string Error { get => error; private set => Set(ref error, value); }
    public string ActionLabel => IsRegistration ? "Создать учетную запись" : "Войти";
    public string ToggleLabel => IsRegistration ? "Вернуться ко входу" : "Регистрация";
    public AsyncCommand Submit { get; }
    public RelayCommand Toggle { get; }
    public AuthViewModel(IAuthService service, Action success)
    {
        this.service = service; this.success = success;
        Toggle = new(_ => { IsRegistration = !IsRegistration; Error = ""; }, () => Submit?.CanExecute(null) ?? true);
        Submit = new(SubmitAsync);
    }
    private async Task SubmitAsync()
    {
        try
        {
            Error = "";
            if (IsRegistration) await service.RegisterAsync(Login, Name, Password, Repeat);
            await service.LoginAsync(Login, Password); Password = ""; Repeat = ""; success();
        }
        catch (BusinessException ex) { Error = ex.Message; }
        catch (Exception ex) { App.Log(ex); Error = "Не удалось войти или сохранить учетную запись. Проверьте доступ к файлу базы данных."; }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Workbench;

public partial class AccountWindow : Window
{
    private readonly LocalAccount? _account;
    private string? _pendingAvatarPath;

    public LocalAccount? SignedInAccount { get; private set; }
    public bool LoggedOut { get; private set; }

    public AccountWindow(LocalAccount? account)
    {
        InitializeComponent();
        _account = account;
        if (account is null) return;
        AuthPanel.Visibility = Visibility.Collapsed;
        ProfilePanel.Visibility = Visibility.Visible;
        ProfileUsername.Text = "账户：" + account.Username;
        ProfileDisplayName.Text = account.DisplayName;
        UpdateAvatar(account.AvatarPath, account.DisplayName);
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        LoginMessage.Text = string.Empty;
        var account = AccountStore.Authenticate(LoginUsername.Text, LoginPassword.Password);
        if (account is null) { LoginMessage.Text = "账户名或密码不正确。"; return; }
        SignedInAccount = account;
        DialogResult = true;
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        RegisterMessage.Text = string.Empty;
        if (RegisterPassword.Password != RegisterPasswordAgain.Password)
        {
            RegisterMessage.Text = "两次输入的密码不一致。";
            return;
        }
        try
        {
            SignedInAccount = AccountStore.Register(RegisterUsername.Text, RegisterPassword.Password, RegisterDisplayName.Text);
            DialogResult = true;
        }
        catch (Exception ex) { RegisterMessage.Text = ex.Message; }
    }

    private void ChooseAvatar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择头像", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true) return;
        _pendingAvatarPath = dialog.FileName;
        UpdateAvatar(_pendingAvatarPath, ProfileDisplayName.Text);
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null) return;
        ProfileMessage.Text = string.Empty;
        try
        {
            _account.DisplayName = string.IsNullOrWhiteSpace(ProfileDisplayName.Text) ? _account.Username : ProfileDisplayName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(_pendingAvatarPath)) _account.AvatarPath = AccountStore.ImportAvatar(_account.Id, _pendingAvatarPath);
            AccountStore.Save(_account);
            SignedInAccount = _account;
            DialogResult = true;
        }
        catch (Exception ex) { ProfileMessage.Text = ex.Message; }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        LoggedOut = true;
        DialogResult = true;
    }

    private void UpdateAvatar(string? path, string fallback)
    {
        ProfileInitial.Text = string.IsNullOrWhiteSpace(fallback) ? "用" : fallback.Trim()[0].ToString();
        ProfileInitial.Visibility = Visibility.Visible;
        ProfileAvatar.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 234, 255));
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var avatarPath = path;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(avatarPath, UriKind.Absolute);
            image.EndInit();
            ProfileAvatar.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            ProfileInitial.Visibility = Visibility.Collapsed;
        }
        catch { }
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Workbench;

public sealed class LocalAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ToolboxPreferences Preferences { get; set; } = new();
}

internal sealed class LocalAccountDatabase
{
    public List<LocalAccount> Accounts { get; set; } = new();
}

public static class AccountStore
{
    private const int Iterations = 120_000;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly string AccountPath = Path.Combine(StateStore.DirectoryPath, "accounts.json");
    private static readonly string AvatarDirectory = Path.Combine(StateStore.DirectoryPath, "avatars");

    public static LocalAccount? Find(Guid? id)
    {
        if (id is null) return null;
        lock (Gate) return LoadDatabase().Accounts.FirstOrDefault(account => account.Id == id.Value);
    }

    public static LocalAccount Register(string username, string password, string displayName)
    {
        username = username.Trim();
        displayName = displayName.Trim();
        ValidateCredentials(username, password);
        lock (Gate)
        {
            var database = LoadDatabase();
            if (database.Accounts.Any(account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("该账户已经存在。");
            var salt = RandomNumberGenerator.GetBytes(16);
            var account = new LocalAccount
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(HashPassword(password, salt))
            };
            database.Accounts.Add(account);
            SaveDatabase(database);
            return account;
        }
    }

    public static LocalAccount? Authenticate(string username, string password)
    {
        username = username.Trim();
        lock (Gate)
        {
            var account = LoadDatabase().Accounts.FirstOrDefault(item => string.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase));
            if (account is null) return null;
            try
            {
                var salt = Convert.FromBase64String(account.PasswordSalt);
                var expected = Convert.FromBase64String(account.PasswordHash);
                var actual = HashPassword(password, salt);
                return CryptographicOperations.FixedTimeEquals(actual, expected) ? account : null;
            }
            catch { return null; }
        }
    }

    public static void Save(LocalAccount account)
    {
        lock (Gate)
        {
            var database = LoadDatabase();
            var index = database.Accounts.FindIndex(item => item.Id == account.Id);
            if (index < 0) database.Accounts.Add(account); else database.Accounts[index] = account;
            SaveDatabase(database);
        }
    }

    public static string ImportAvatar(Guid accountId, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".bmp")
            throw new InvalidOperationException("请选择 PNG、JPG 或 BMP 图片。");
        Directory.CreateDirectory(AvatarDirectory);
        var destination = Path.Combine(AvatarDirectory, accountId.ToString("N") + extension);
        File.Copy(sourcePath, destination, true);
        return destination;
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

    private static void ValidateCredentials(string username, string password)
    {
        if (username.Length is < 2 or > 32 || username.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("账户名需为 2–32 个字符，且不能包含空格。");
        if (password.Length is < 6 or > 128)
            throw new InvalidOperationException("密码至少 6 位，最多 128 位。");
    }

    private static LocalAccountDatabase LoadDatabase()
    {
        try
        {
            if (!File.Exists(AccountPath)) return new LocalAccountDatabase();
            return JsonSerializer.Deserialize<LocalAccountDatabase>(File.ReadAllText(AccountPath), Options) ?? new LocalAccountDatabase();
        }
        catch { return new LocalAccountDatabase(); }
    }

    private static void SaveDatabase(LocalAccountDatabase database)
    {
        Directory.CreateDirectory(StateStore.DirectoryPath);
        var temporaryPath = AccountPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(database, Options));
        File.Move(temporaryPath, AccountPath, true);
    }
}

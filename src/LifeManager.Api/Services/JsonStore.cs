using System.Text.Json;
using LifeManager.Api.Domain;

namespace LifeManager.Api.Services;

public sealed class JsonStore
{
    private readonly string _root;
    private readonly string _accountsPath;
    private readonly string _dataDir;
    private readonly string _receiptsDir;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["App:DataPath"] ?? "App_Data";
        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
        _accountsPath = Path.Combine(_root, "accounts.json");
        _dataDir = Path.Combine(_root, "users");
        _receiptsDir = Path.Combine(_root, "receipts");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_receiptsDir);
        if (!File.Exists(_accountsPath)) File.WriteAllText(_accountsPath, "[]");
    }

    public async Task<List<UserAccount>> GetAccountsAsync()
    {
        await _gate.WaitAsync();
        try { return await ReadAsync<List<UserAccount>>(_accountsPath) ?? []; }
        finally { _gate.Release(); }
    }

    public async Task<UserAccount?> FindAccountAsync(string email)
        => (await GetAccountsAsync()).FirstOrDefault(x => string.Equals(x.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task AddAccountAsync(UserAccount account)
    {
        await _gate.WaitAsync();
        try
        {
            var accounts = await ReadAsync<List<UserAccount>>(_accountsPath) ?? [];
            accounts.Add(account);
            await WriteAsync(_accountsPath, accounts);
        }
        finally { _gate.Release(); }
    }


    public async Task UpdateAccountDisplayNameAsync(Guid userId, string displayName)
    {
        await _gate.WaitAsync();
        try
        {
            var accounts = await ReadAsync<List<UserAccount>>(_accountsPath) ?? [];
            var account = accounts.FirstOrDefault(x => x.Id == userId);
            if (account is null) return;
            account.DisplayName = displayName;
            await WriteAsync(_accountsPath, accounts);
        }
        finally { _gate.Release(); }
    }

    public async Task<LifeData> GetDataAsync(Guid userId)
    {
        var path = DataPath(userId);
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(path))
            {
                var data = CreateEmptyData();
                await WriteAsync(path, data);
                return data;
            }
            return await ReadAsync<LifeData>(path) ?? CreateEmptyData();
        }
        finally { _gate.Release(); }
    }

    public async Task SaveDataAsync(Guid userId, LifeData data)
    {
        await _gate.WaitAsync();
        try { await WriteAsync(DataPath(userId), data); }
        finally { _gate.Release(); }
    }

    public async Task SeedDemoAsync(Guid userId, string displayName)
    {
        var data = DemoData.Create(displayName);
        await SaveDataAsync(userId, data);
    }

    public async Task<string> SaveReceiptAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (ext.Length > 8) ext = "";
        var name = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{ext}";
        var userDir = Path.Combine(_receiptsDir, userId.ToString("N"));
        Directory.CreateDirectory(userDir);
        var path = Path.Combine(userDir, name);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream);
        return Path.Combine("receipts", userId.ToString("N"), name).Replace('\\', '/');
    }

    private string DataPath(Guid userId) => Path.Combine(_dataDir, $"{userId:N}.json");
    private static LifeData CreateEmptyData() => new();

    private async Task<T?> ReadAsync<T>(string path)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(stream, _json);
    }

    private async Task WriteAsync<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp)) await JsonSerializer.SerializeAsync(stream, value, _json);
        File.Move(tmp, path, true);
    }
}

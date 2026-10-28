using System.Security.Cryptography;
using System.Text;
using HomeMenu.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HomeMenu.Infrastructure;

public sealed class DatabaseRuntime
{
    private readonly DbContextOptions<AppDbContext> options;
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal long Revision { get; set; }
    public string Path { get; }
    public TimeProvider Clock { get; }
    public DateOnly Today => DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);
    public DatabaseRuntime(string path, TimeProvider? clock = null, params IInterceptor[] interceptors)
    {
        Path = System.IO.Path.GetFullPath(path); Clock = clock ?? TimeProvider.System;
        var connection = new SqliteConnectionStringBuilder { DataSource = Path, ForeignKeys = true, DefaultTimeout = 5, Pooling = false };
        options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection.ToString()).AddInterceptors(interceptors).Options;
    }
    public AppDbContext CreateContext() => new(options);
    public static string DefaultPath => Environment.GetEnvironmentVariable("HOMEMENU_DATABASE_PATH")
        ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeMenuCourse", "homemenu.db");
    public async Task InitializeAsync()
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
            await SeedData.ApplyAsync(db);
        }
        finally { Gate.Release(); }
    }
}

/// <summary>Must be acquired and disposed by the application UI thread.</summary>
public sealed class InstanceLease : IDisposable
{
    private readonly Mutex mutex;
    private bool owned;
    public InstanceLease(string path)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(path).ToUpperInvariant())));
        mutex = new Mutex(false, @"Local\HomeMenu_" + key);
        try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
        if (!owned) { mutex.Dispose(); throw new BusinessException("Приложение для этого файла базы данных уже запущено."); }
    }
    public void Dispose() { if (owned) { owned = false; mutex.ReleaseMutex(); mutex.Dispose(); } }
}

public sealed record SessionTicket(int UserId, string DisplayName);
public sealed class UserSession
{
    private SessionTicket? current;
    public SessionTicket Require() => Volatile.Read(ref current) ?? throw new BusinessException("Войдите в учетную запись.");
    internal void Set(User user) => Volatile.Write(ref current, new SessionTicket(user.Id, user.DisplayName));
    public void Clear() => Volatile.Write(ref current, null);
    internal void Verify(SessionTicket ticket)
    {
        if (!ReferenceEquals(ticket, Volatile.Read(ref current))) throw new BusinessException("Сеанс изменился. Войдите заново.");
    }
}

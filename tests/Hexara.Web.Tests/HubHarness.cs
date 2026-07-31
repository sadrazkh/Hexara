using System.Security.Claims;
using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Domain.Game;
using Hexara.Infrastructure.Identity;
using Hexara.Web.Realtime;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Web.Tests;

/// <summary>
/// هابِ واقعی، با همان سیم‌کشیِ واقعیِ برنامه — فقط چیزهایی که خودِ SignalR سرِ
/// اجرا تزریق می‌کند (گروه‌ها، گیرنده‌ها، هویت اتصال) بدلی‌اند.
///
/// **چرا این بستر لازم بود:** دو ادعای امنیتیِ این پروژه فقط با بازخوانی کد
/// تأیید شده بودند و هیچ آزمونی نداشتند — «تماشاچی به گروه بازیکن‌ها راه ندارد»
/// و «چت فقط به بازیکن‌ها می‌رسد». هر دو به *عضویتِ گروه* بستگی دارند، و عضویت
/// گروه چیزی است که فقط در رفتارِ هاب دیده می‌شود، نه در امضای هیچ متدی.
///
/// عمداً بدون کتابخانه‌ی mock نوشته شده: بدل‌ها سه تا بیشتر نیستند و افزودنِ یک
/// وابستگی برای سه کلاس، به همان قاعده‌ای می‌خورد که برای بقیه‌ی پروژه گذاشته‌ایم.
/// </summary>
internal sealed class HubHarness : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    private HubHarness(IServiceScope scope, GameHub hub, FakeGroups groups, FakeClients clients)
    {
        _scope = scope;
        Hub = hub;
        Groups = groups;
        Clients = clients;
    }

    public GameHub Hub { get; }

    /// <summary>چه کسی به کدام گروه اضافه یا از آن برداشته شد.</summary>
    public FakeGroups Groups { get; }

    /// <summary>چه پیامی به کدام مقصد رفت.</summary>
    public FakeClients Clients { get; }

    public static HubHarness For(HexaraApp app, Guid userId, string connectionId = "conn-1")
    {
        var scope = app.Services.CreateScope();

        var groups = new FakeGroups();
        var clients = new FakeClients();

        var hub = ActivatorUtilities.CreateInstance<GameHub>(scope.ServiceProvider);
        hub.Groups = groups;
        hub.Clients = clients;
        hub.Context = new FakeContext(connectionId, userId);

        return new HubHarness(scope, hub, groups, clients);
    }

    /// <summary>
    /// یک بازیِ واقعی در دیتابیسِ همین برنامه می‌سازد.
    ///
    /// کاربرها هم واقعاً ساخته می‌شوند: جدول بازی به آن‌ها کلید خارجی دارد و بی
    /// این کار ذخیره رد می‌شود — که خودش نشانه‌ی خوبی است، یعنی بستر دارد با
    /// همان محدودیت‌های واقعی کار می‌کند نه با یک دیتابیسِ سست.
    /// </summary>
    public async Task<Guid> NewGameAsync(IReadOnlyList<Guid> playerIds)
    {
        await EnsureUsersAsync(playerIds);

        var repository = _scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var options = new GameOptions { PlayerCount = playerIds.Count, Seed = 5 };

        return await repository.CreateAsync(
            GameState.Create(options, playerIds),
            playerIds,
            GameStatus.Active);
    }

    /// <summary>کاربرهای نبوده را به‌عنوان مهمان می‌سازد.</summary>
    public async Task EnsureUsersAsync(IEnumerable<Guid> userIds)
    {
        var users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        foreach (var id in userIds.Distinct())
        {
            if (await users.FindByIdAsync(id.ToString()) is not null)
            {
                continue;
            }

            var created = await users.CreateAsync(new AppUser
            {
                Id = id,
                UserName = $"test-{id:N}",
                DisplayName = $"بازیکن {id.ToString()[..4]}",
                AvatarColor = "#336699",
                IsGuest = true
            });

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"ساخت کاربر آزمایشی ناموفق بود: {string.Join(", ", created.Errors.Select(e => e.Description))}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Hub.Dispose();
        _scope.Dispose();
        await ValueTask.CompletedTask;
    }

    // ── بدل‌ها ───────────────────────────────────────────────────────────

    internal sealed class FakeGroups : IGroupManager
    {
        private readonly List<(string Connection, string Group)> _added = [];
        private readonly List<(string Connection, string Group)> _removed = [];

        public IReadOnlyList<string> GroupsOf(string connectionId) =>
            [.. _added.Where(x => x.Connection == connectionId).Select(x => x.Group)];

        public bool Removed(string connectionId, string group) =>
            _removed.Contains((connectionId, group));

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            _added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            _removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    /// <summary>یک پیامِ فرستاده‌شده و مقصدش.</summary>
    internal sealed record Sent(string Target, string Method, object?[] Args);

    internal sealed class FakeClients : IHubCallerClients
    {
        public List<Sent> Messages { get; } = [];

        /// <summary>پیام‌هایی که به یک گروه رفته‌اند.</summary>
        public IEnumerable<Sent> ToGroup(string group) =>
            Messages.Where(m => m.Target == $"group:{group}");

        private IClientProxy Proxy(string target) => new FakeProxy(target, Messages);

        public IClientProxy Caller => Proxy("caller");

        public IClientProxy Others => Proxy("others");

        public IClientProxy All => Proxy("all");

        public IClientProxy AllExcept(IReadOnlyList<string> excluded) => Proxy("allExcept");

        public IClientProxy Client(string connectionId) => Proxy($"client:{connectionId}");

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");

        public IClientProxy Group(string groupName) => Proxy($"group:{groupName}");

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excluded) =>
            Proxy($"groupExcept:{groupName}");

        public IClientProxy OthersInGroup(string groupName) => Proxy($"othersInGroup:{groupName}");

        public IClientProxy User(string userId) => Proxy($"user:{userId}");

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");

        private sealed class FakeProxy(string target, List<Sent> sink) : IClientProxy
        {
            public Task SendCoreAsync(
                string method,
                object?[] args,
                CancellationToken cancellationToken = default)
            {
                sink.Add(new Sent(target, method, args));
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FakeContext(string connectionId, Guid userId) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;

        public override string? UserIdentifier => userId.ToString();

        public override ClaimsPrincipal? User { get; } = new(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}

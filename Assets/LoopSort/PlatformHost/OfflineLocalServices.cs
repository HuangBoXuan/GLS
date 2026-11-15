using System;
using MiniGamePlatform.HotUpdate;
using MiniGamePlatform.SDK;
using MiniGamePlatform.SDK.Auth;
using MiniGamePlatform.SDK.Contracts;
using MiniGamePlatform.SDK.Host;

namespace LoopSort.PlatformLocalHost
{
    // An explicitly offline device profile. This is not a PlatformSession,
    // registration, login token, cloud identity, advertisement or paid entitlement.
    public sealed class OfflineLocalServiceFactory : IModuleHostServiceFactory
    {
        readonly AtomicLocalStore store;
        readonly string profile;
        public OfflineLocalServiceFactory(string root, string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || !profileId.StartsWith("local-only:", StringComparison.Ordinal))
                throw new ArgumentException("A local-only profile is required.", nameof(profileId));
            store = new AtomicLocalStore(root); profile = profileId;
        }
        public IModuleHostServices CreateServices(ModuleManifest manifest, ProductContext product, PlatformSession session)
        {
            if (session != null) throw new InvalidOperationException("The offline shell must not impersonate a platform session.");
            if (manifest == null || product == null || !product.AllowsGame(manifest.gameId))
                throw new InvalidOperationException("The module is not allowed by this local shell.");
            return new Scope(store, profile, manifest.gameId);
        }

        sealed class Scope : IModuleHostServices
        {
            internal readonly AtomicLocalStore Store;
            internal readonly string User, Game;
            internal bool Closed;
            public IModuleAccount Account { get; }
            public IModuleLocalStorage Storage { get; }
            public IModuleAds Ads { get; } = new NoAds();
            public IModuleRequests Requests { get; } = new NoRequests();
            public Scope(AtomicLocalStore store, string user, string game)
            { Store = store; User = user; Game = game; Account = new AccountView(this); Storage = new StorageView(this); }
            internal void Require() { if (Closed) throw new ObjectDisposedException("Local game identity"); }
            public void Dispose()
            { if (Closed) return; Closed = true; ((AccountView)Account).Detach(); }
        }
        sealed class AccountView : IModuleAccount
        {
            readonly Scope scope;
            Action invalidated;
            public AccountView(Scope scope) { this.scope = scope; }
            public bool IsAvailable => !scope.Closed;
            public string UserId => IsAvailable ? scope.User : null;
            public event Action Invalidated
            {
                add { scope.Require(); invalidated += value; }
                remove { invalidated -= value; }
            }
            internal void Detach() { invalidated = null; }
        }
        sealed class StorageView : IModuleLocalStorage
        {
            readonly Scope scope;
            public StorageView(Scope scope) { this.scope = scope; }
            public bool IsAvailable => !scope.Closed;
            public string Read(string key) { scope.Require(); return scope.Store.Read(scope.User, scope.Game, key); }
            public void Write(string key, string value) { scope.Require(); scope.Store.Write(scope.User, scope.Game, key, value); }
            public void Delete(string key) { scope.Require(); scope.Store.Delete(scope.User, scope.Game, key); }
        }
        sealed class NoAds : IModuleAds, IModuleAdReadiness
        {
            public bool IsAvailable => false;
            public bool IsReady(string placement, string format) => false;
            public string Show(string placement, string format, Action<string> callback)
                => throw new InvalidOperationException("LOCAL_ONLY_ADS_UNAVAILABLE");
        }
        sealed class NoRequests : IModuleRequests
        {
            public bool IsAvailable => false;
            public string Send(string json, Action<string> callback)
                => throw new InvalidOperationException("LOCAL_ONLY_NETWORK_UNAVAILABLE");
            public void Cancel(string requestId) { }
        }
    }
}

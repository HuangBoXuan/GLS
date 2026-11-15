using System;
using System.Collections.Generic;
using System.IO;
using AppsFlyerSDK;
using MiniGamePlatform.SDK.Host;
using MiniGamePlatform.SDK.Auth;
using UnityEngine;

namespace LoopSort.ProductIntegration
{
    // Product-owned service. Not a game prefab and never part of its AssetBundle.
    public sealed partial class ProductAdvertising : MonoBehaviour, IHostAdDriver, IHostAdFullscreenState, IHostBannerDriver, IHostPrivacyDriver
    {
        sealed class Pending
        { public string Id, Unit, Format; public bool Reward; public Action<string, string> Terminal; }
        static ProductAdvertising owner;
        ProductConfiguration config;
        ProductConsent consent;
        HostMainThreadQueue queue;
        Pending pending;
        PlatformSession session;
        bool initialized, subscribed, afStarted, disposed, accountInvalid, nativeTestMode;
        float nextInterstitialAt;
        string bannerUnit;
        bool bannerRequested;
        bool bannerReady;
        HostNativeCoordinator nativeCoordinator;
        readonly Dictionary<string, float> retryAt = new Dictionary<string, float>();
        readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        readonly Dictionary<string, string> units = new Dictionary<string, string>();
        readonly HashSet<string> loaded = new HashSet<string>();
        public bool IsTest => nativeTestMode || (config != null && config.developmentOnly);
        public bool IsFullscreenShowing => pending != null;
        public bool Initialized => initialized;

        public CommonModuleServiceFactory CreateFactory(ProductConfiguration configuration, ProductConsent privacy, PlatformSession account = null)
        {
            if (config != null) throw new InvalidOperationException("Product services are already configured.");
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            consent = privacy ?? throw new ArgumentNullException(nameof(privacy));
            queue = new HostMainThreadQueue();
            session = account;
            if (config.maxEnabled || config.appsFlyerEnabled)
            {
                var missing = config.MissingReleaseFields();
                if (missing.Count != 0) throw new InvalidOperationException("Product configuration incomplete: " + string.Join(", ", missing));
                if (config.androidPackageName != Application.identifier) throw new InvalidOperationException("Product package name differs.");
                if (!consent.Complete) throw new InvalidOperationException("Complete the product consent flow first.");
                if (config.appsFlyerEnabled && string.IsNullOrWhiteSpace(session?.UserId))
                    throw new InvalidOperationException("Attribution requires the authenticated product identity.");
                if (owner != null && owner != this) throw new InvalidOperationException("Another product owns native advertising.");
                owner = this;
                if (session != null) session.IdentityChanged += AccountChanged;
                DontDestroyOnLoad(gameObject);
                if (config.maxEnabled && consent.AllowAds) InitializeMax();
                else StartAttribution();
            }
            var broker = new HostAdBroker(config.maxEnabled && consent.AllowAds ? this : null, queue);
            nativeCoordinator=new HostNativeCoordinator(broker,config.maxEnabled&&consent.AllowAds?this:null,this);
            return new CommonModuleServiceFactory(new AtomicLocalStore(Path.Combine(Application.persistentDataPath,
                "ProductSaves")), broker,nativeCoordinator);
        }

        void InitializeMax(bool automaticConsent=false)
        {
            if(!string.IsNullOrWhiteSpace(session?.UserId))MaxSdk.SetUserId(session.UserId);
            foreach (var p in config.placements)
            {
                if (units.TryGetValue(p.adUnitId, out var format) && format != p.format)
                    throw new InvalidOperationException("An ad unit cannot serve different formats.");
                units[p.adUnitId] = p.format;
            }
            if(!subscribed)Subscribe();
            if(!automaticConsent)
            {
                MaxSdk.SetHasUserConsent(consent.PersonalizedAds);
                MaxSdk.SetDoNotSell(consent.DoNotSell);
            }
#pragma warning disable 618
            MaxSdk.SetSdkKey(config.maxSdkKey);
#pragma warning restore 618
            MaxSdk.InitializeSdk();
        }

        void StartAttribution()
        {
            if (afStarted || accountInvalid || !config.appsFlyerEnabled || !consent.AllowMeasurement) return;
            AppsFlyer.setIsDebug(false);
            AppsFlyer.setConsentData(new AppsFlyerConsent(consent.SubjectToGdpr, consent.DataUsage,
                consent.PersonalizedAds, consent.AdStorage));
            AppsFlyer.initSDK(config.appsFlyerDevKey, "");
            AppsFlyer.setCustomerUserId(session.UserId);
            AppsFlyer.stopSDK(false);
            AppsFlyer.startSDK();
            afStarted = true;
        }

        void AccountChanged() => queue.Post(() =>
        {
            if (disposed) return;
            accountInvalid = true;
            if (afStarted) { AppsFlyer.stopSDK(true); afStarted = false; }
            // The SDK invalidates module services. Keep native fullscreen locked
            // until Hidden; its old request must never reward the new account.
            if (pending != null) pending.Reward = false;
            bannerRequested=false;UpdateBanner();
        });

        void Subscribe()
        {
            MaxSdkCallbacks.OnSdkInitializedEvent += SdkReady;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += Loaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += Loaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += LoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += LoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += Reward;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += Hidden;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += Hidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += DisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += DisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += Revenue;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += Revenue;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += Revenue;
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += Loaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += LoadFailed;
            subscribed = true;
        }

        void SdkReady(MaxSdkBase.SdkConfiguration sdk) => queue.Post(() =>
        {
            if (disposed) return;
            nativeTestMode = sdk.IsTestModeEnabled;
            if(standalone){StandaloneSdkReady(sdk);return;}
            if(!sdk.IsSuccessfullyInitialized){Report("sdk_initialization_failed");return;}
            initialized = true; StartAttribution();
            int i = 0;
            foreach (var unit in units)if(unit.Value!="banner")retryAt[unit.Key] = Time.realtimeSinceStartup + i++ * 1.5f;
            EnsureBanner();UpdateBanner();
        });
        void Loaded(string unit, MaxSdkBase.AdInfo _) => queue.Post(() =>
        { if (!disposed && units.ContainsKey(unit)) { attempts[unit] = 0; loaded.Add(unit); retryAt.Remove(unit);if(unit==bannerUnit)bannerReady=true;if(units[unit]=="rewarded")Status="广告已准备好";Report(units[unit]+" loaded"); } });
        void LoadFailed(string unit, MaxSdkBase.ErrorInfo error) => queue.Post(() =>
        {
            if (disposed || !units.ContainsKey(unit)) return;
            if(unit==bannerUnit)bannerReady=false;
            loaded.Remove(unit);
            attempts.TryGetValue(unit, out int n); attempts[unit] = Math.Min(n + 1, 6);
            retryAt[unit] = Time.realtimeSinceStartup + Mathf.Min(60, Mathf.Pow(2, attempts[unit]));
            Status="广告加载中，请稍后重试";Report(units[unit]+" load_failed code="+(error==null?"exception":((int)error.Code).ToString())+" networkCode="+(error?.MediatedNetworkErrorCode??0));
        });
        void Reward(string unit, MaxSdkBase.Reward _, MaxSdkBase.AdInfo info) => queue.Post(() =>
        { if (!disposed && pending?.Unit == unit && pending.Format == "rewarded") {pending.Reward = true;Report("reward callback received");} });
        void Hidden(string unit, MaxSdkBase.AdInfo _) => queue.Post(() =>
        {
            if (disposed || pending?.Unit != unit) return;
            string status = !accountInvalid && (pending.Format == "interstitial" || pending.Reward) ? "completed" : "cancelled";
            if(pending.Format=="interstitial"&&!accountInvalid)nextInterstitialAt=Time.realtimeSinceStartup+config.interstitialCooldownSeconds;
            Report(pending.Format+" hidden "+status);
            Finish(status, null); Schedule(unit);
            UpdateBanner();
        });
        void DisplayFailed(string unit, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info) => queue.Post(() =>
        { if (!disposed && pending?.Unit == unit) { Report("display_failed code="+(error==null?"exception":((int)error.Code).ToString()));Finish("failed", "display_failed"); Schedule(unit); UpdateBanner(); } });
        void Revenue(string unit, MaxSdkBase.AdInfo info) => queue.Post(() =>
        {
            if (disposed || !afStarted || !config.adRevenueEnabled || !units.ContainsKey(unit) || info == null ||
                info.Revenue < 0 || double.IsNaN(info.Revenue) || double.IsInfinity(info.Revenue)) return;
            var values = new Dictionary<string, string> { { "ad_unit", unit }, { "ad_type", info.AdFormat ?? "" },
                { "placement", info.Placement ?? "" }, { "creative_id", info.CreativeIdentifier ?? "" },
                { "network_placement", info.NetworkPlacement ?? "" }, { "revenue_precision", info.RevenuePrecision ?? "" } };
            AppsFlyer.logAdRevenue(new AFAdRevenueData(info.NetworkName ?? "", MediationNetwork.ApplovinMax, "USD", info.Revenue), values);
        });

        void Schedule(string unit) { loaded.Remove(unit); retryAt[unit] = Time.realtimeSinceStartup + 1; }
        public bool IsReady(string gameId, string placement, string format)
        {
            queue.RequireMainThread();
            if(format!="rewarded"&&format!="interstitial")return false;
            var p = config.Find(gameId, placement, format);
            if (format == "interstitial" && Time.realtimeSinceStartup < nextInterstitialAt) return false;
            return !disposed && !accountInvalid && initialized && pending == null && p != null && loaded.Contains(p.adUnitId) &&
                (format == "rewarded" ? MaxSdk.IsRewardedAdReady(p.adUnitId) : MaxSdk.IsInterstitialReady(p.adUnitId));
        }
        public void Show(string requestId, string gameId, string placement, string format, Action<string, string> terminal)
        {
            if (!IsReady(gameId, placement, format)) { terminal("not_ready", null); return; }
            var p = config.Find(gameId, placement, format);
            pending = new Pending { Id = requestId, Unit = p.adUnitId, Format = format, Terminal = terminal };
            Report(format+" show_requested placement="+placement);
            UpdateBanner();
            loaded.Remove(p.adUnitId);
            try
            {
                if (format == "rewarded") MaxSdk.ShowRewardedAd(p.adUnitId, placement, requestId);
                else
                {
                    MaxSdk.ShowInterstitial(p.adUnitId, placement, requestId);
                }
            }
            catch { Finish("failed", "native_show_failed"); Schedule(p.adUnitId); }
        }
        void Finish(string status, string error)
        {
            var request = pending; pending = null;
            try { request?.Terminal?.Invoke(status, error); }
            catch (Exception e) { Debug.LogWarning("Product ad callback failed: " + e.GetType().Name); }
        }
        // The product host calls this only after reserving a non-overlapping
        // banner area. A configured banner never covers gameplay by default.
        public void SetBannerVisible(bool visible)
        {
            if(queue==null||disposed)return;
            queue.RequireMainThread();bannerRequested=visible;UpdateBanner();
        }
        bool IHostBannerDriver.IsReady=>initialized&&!disposed&&!accountInvalid&&bannerReady;
        public bool Show(string placement)
        {
            if(placement!="home"&&placement!="gameplay")return false;
            if(!((IHostBannerDriver)this).IsReady)return false;
            SetBannerVisible(true);return bannerRequested;
        }
        public void Hide()=>SetBannerVisible(false);
        public bool TryGetGeometry(out Rect screen,out Rect safe,out Rect banner,out float density)
        {
            screen=new Rect(0,0,Screen.width,Screen.height);safe=Screen.safeArea;banner=default;density=0;
            if(!((IHostBannerDriver)this).IsReady||bannerUnit==null)return false;
            density=MaxSdkUtils.GetScreenDensity();
            if(density<=0||float.IsNaN(density)||float.IsInfinity(density))return false;
            // The pinned Android plugin returns dp with a top-left origin;
            // SDK HostNativeCoordinator requires physical bottom-left pixels.
            var layout=MaxSdk.GetBannerLayout(bannerUnit);
            banner=new Rect(layout.x*density,Screen.height-(layout.y+layout.height)*density,layout.width*density,layout.height*density);
            return banner.width>0&&banner.height>0;
        }
        public bool OpenDocument(string document)
        {
            string url=document=="privacy"?config.privacyPolicyUrl:document=="agreement"?config.termsUrl:document=="data_deletion"?config.accountDeletionUrl:null;
            if(!ProductConfiguration.Https(url))return false;
            Application.OpenURL(url);return true;
        }
        public bool ShowOptions(Action<bool> completion)=>standalone&&ShowStandalonePrivacy(completion);
        void EnsureBanner()
        {
            if(!initialized||disposed||accountInvalid||!consent.AllowAds||bannerUnit!=null)return;
            var placement=config.Find(config.gameId,"home_banner","banner");
            if(placement==null)return;
            bannerUnit=placement.adUnitId;
            MaxSdk.CreateBanner(bannerUnit,new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter));
            MaxSdk.SetBannerPlacement(bannerUnit,placement.placement);
            MaxSdk.HideBanner(bannerUnit);MaxSdk.StopBannerAutoRefresh(bannerUnit);
        }
        void UpdateBanner()
        {
            bool show=bannerRequested&&initialized&&!disposed&&!accountInvalid&&consent.AllowAds&&pending==null;
            if(show)EnsureBanner();
            if(bannerUnit==null)return;
            if(show){MaxSdk.ShowBanner(bannerUnit);MaxSdk.StartBannerAutoRefresh(bannerUnit);}
            else{MaxSdk.HideBanner(bannerUnit);MaxSdk.StopBannerAutoRefresh(bannerUnit);}
        }
        void Update()
        {
            queue?.Drain();
            if(standalone&&!initialized&&!privacyBusy&&Time.realtimeSinceStartup>=privacyRetryAt)RetryAvailability();
            if (!initialized || disposed || accountInvalid || pending != null) return;
            string due = null;
            foreach (var item in retryAt) if (item.Value <= Time.realtimeSinceStartup) { due = item.Key; break; }
            if (due == null) return;
            retryAt.Remove(due);
            try { if (units[due] == "rewarded") MaxSdk.LoadRewardedAd(due); else if(units[due]=="banner")MaxSdk.LoadBanner(due);else MaxSdk.LoadInterstitial(due); }
            catch { LoadFailed(due, null); }
        }
        void OnDestroy()
        {
            disposed = true; Finish("cancelled", "product_closed");
            if(bannerUnit!=null){MaxSdk.DestroyBanner(bannerUnit);bannerUnit=null;}
            if (session != null) session.IdentityChanged -= AccountChanged;
            if (afStarted) { AppsFlyer.stopSDK(true); afStarted = false; }
            if (owner == this) owner = null;
            if (!subscribed) return;
            MaxSdkCallbacks.OnSdkInitializedEvent -= SdkReady;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= Loaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= Loaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= LoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= LoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= Reward;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= Hidden;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= Hidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= DisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= DisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= Revenue;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= Revenue;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= Revenue;
            MaxSdkCallbacks.Banner.OnAdLoadedEvent -= Loaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= LoadFailed;
        }
        void OnApplicationPause(bool paused)=>nativeCoordinator?.SetApplicationPaused(paused);
    }
}

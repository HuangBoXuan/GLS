using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort.ProductIntegration
{
    public sealed partial class ProductAdvertising
    {
        bool standalone, sdkConnected, privacyBusy, sdkInitializing;
        float privacyRetryAt;
        public string Status {get;private set;}="广告准备中…";
        public bool RequestsAllowed=>initialized&&!disposed&&!privacyBusy;
        public event Action<string> Diagnostic;
        void Report(string value)
        {
            // Fixed stages only; never log native payloads or credentials.
            Diagnostic?.Invoke(value);
        }
        public void BeginStandalone(ProductConfiguration configuration)
        {
            if(config!=null||owner!=null||MaxSdk.IsInitialized())throw new InvalidOperationException("Advertising already has an owner.");
            var missing=configuration.MissingAdIntegrationFields();
            if(missing.Count!=0)throw new InvalidOperationException("Advertising configuration incomplete: "+string.Join(", ",missing));
            if(configuration.androidPackageName!=Application.identifier)throw new InvalidOperationException("Advertising package differs.");
            config=JsonUtility.FromJson<ProductConfiguration>(JsonUtility.ToJson(configuration));
            // Standalone local play has no authenticated platform measurement identity.
            config.appsFlyerEnabled=false;config.adRevenueEnabled=false;
            var placements=new List<AdPlacement>(config.placements);
            var shared=config.Find(config.gameId,"daily_gift","rewarded");
            foreach(var alias in new[]{"ad_break","booster_undo","booster_shuffle","booster_belt","booster_truck","skip_level","revive"})
                if(config.Find(config.gameId,alias,"rewarded")==null)
                    placements.Add(new AdPlacement{gameId=config.gameId,placement=alias,format="rewarded",adUnitId=shared.adUnitId});
            config.placements=placements.ToArray();
            consent=new ProductConsent();queue=new MiniGamePlatform.SDK.Host.HostMainThreadQueue();
            standalone=true;owner=this;DontDestroyOnLoad(gameObject);
            MaxSdkBase.InvokeEventsOnUnityMainThread=true;
            RequestStandalonePrivacy();
        }
        void StandaloneSdkReady(MaxSdkBase.SdkConfiguration sdk)
        {
            sdkInitializing=false;privacyRetryAt=Time.realtimeSinceStartup+60;
            sdkConnected=sdk.IsSuccessfullyInitialized;
            if(!sdkConnected){Status="广告暂不可用，请稍后重试";Report("sdk_failed");return;}
            RefreshStandaloneEligibility();
        }
        void RefreshStandaloneEligibility()
        {
            bool allowed=false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var ump=new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                using(var info=ump.CallStatic<AndroidJavaObject>("getConsentInformation",activity))
                    allowed=info.Call<bool>("canRequestAds");
            }
            catch {Report("privacy_state_unavailable");}
#endif
            privacyBusy=false;
            // Eligibility is not personalized consent. UMP retains actual choices.
            consent.Complete=allowed;consent.AllowAds=allowed;
            initialized=sdkConnected&&allowed;
            if(!allowed)
            {
                Status="广告暂不可用，请稍后重试";privacyRetryAt=Time.realtimeSinceStartup+60;
                retryAt.Clear();loaded.Clear();bannerRequested=false;UpdateBanner();return;
            }
            if(!sdkConnected)
            {
                Status="广告准备中…";sdkInitializing=true;
                try {InitializeMax(true);}
                catch {sdkInitializing=false;privacyRetryAt=Time.realtimeSinceStartup+60;Status="广告暂不可用，请稍后重试";Report("sdk_start_failed");}
                return;
            }
            Status="广告准备中…";
            int index=0;
            foreach(var unit in units)if(unit.Value!="banner")retryAt[unit.Key]=Time.realtimeSinceStartup+index++*1.5f;
            EnsureBanner();UpdateBanner();Report("ads_eligible");
        }
        public void RetryAvailability()
        {
            if(standalone&&!disposed&&!privacyBusy&&!initialized&&!sdkInitializing&&Time.realtimeSinceStartup>=privacyRetryAt)
                RequestStandalonePrivacy();
        }
        bool ShowStandalonePrivacy(Action<bool> completion)
        {
            if(!standalone||!sdkConnected||privacyBusy||pending!=null)return false;
            privacyBusy=true;initialized=false;retryAt.Clear();loaded.Clear();bannerRequested=false;UpdateBanner();
#if UNITY_ANDROID && !UNITY_EDITOR
            consentDismissed=new FormDismissed(code=>queue.Post(()=>
            {
                if(disposed)return;
                RefreshStandaloneEligibility();completion?.Invoke(code==0);
            }));
            RunAndroidPrivacy(activity=>
            {
                using(var ump=new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                    ump.CallStatic("showPrivacyOptionsForm",activity,consentDismissed);
            });
            return true;
#else
            privacyBusy=false;completion?.Invoke(false);return false;
#endif
        }
    }
}

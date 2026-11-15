using System;
using System.Collections;
using LoopSort.ProductIntegration;
using LoopSort0;
using MiniGamePlatform.SDK.Host;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoopSort.Standalone
{
    // Owns ads for the direct Gameplay player only. Platform shells supply their own driver.
    public sealed class StandaloneAdvertising:MonoBehaviour,IPeripheralCommerceProvider,IPeripheralAdFeatures,IPeripheralSettlementAds,IPeripheralAdLifecycle
    {
        static StandaloneAdvertising instance;
        ProductAdvertising advertising;
        ProductConfiguration config;
        GameController game;
        bool background,bannerVisible;
        public bool RewardedReady=>advertising!=null&&advertising.RequestsAllowed&&advertising.IsReady(config.gameId,"daily_gift","rewarded");
        public string AdStatus=>advertising==null?"广告暂不可用，请稍后重试":advertising.Status=="广告已准备好"?"广告准备中，请稍后重试":advertising.Status;
        public bool PurchasesReady=>false;
        public event Action<string> AdClosed;
        public string PriceFor(string id)=>"";
        public void Purchase(string id,Action<string> done)=>done(null);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(instance!=null||SceneManager.GetActiveScene().name!="Gameplay")return;
            new GameObject("LoopSort advertising").AddComponent<StandaloneAdvertising>();
#endif
        }
        IEnumerator Start()
        {
            instance=this;DontDestroyOnLoad(gameObject);
            try
            {
                config=ProductConfiguration.LoadBundled();
                advertising=gameObject.AddComponent<ProductAdvertising>();
                advertising.BeginStandalone(config);
            }
            catch(Exception error)
            {
                Debug.LogWarning("Advertising startup unavailable: "+error.GetType().Name);
                if(advertising!=null)Destroy(advertising);advertising=null;
            }
            while(GameController.Instance==null||GameController.Instance.Hud==null||GameController.Instance.Progress==null)yield return null;
            game=GameController.Instance;game.SetCommerceProvider(this);
        }
        public void RetryAdvertising()=>advertising?.RetryAvailability();
        public bool ShowPrivacyOptions(Action<bool> done)=>advertising!=null&&advertising.ShowOptions(done);
        public void ContinueWithInterstitial(Action done)
        {
            done(); // Compatibility: navigation never creates an ad opportunity.
        }
        public void ShowSettlementInterstitial(Action<bool> done)
        {
            if(advertising==null||!advertising.RequestsAllowed||!advertising.IsReady(config.gameId,"level_complete","interstitial")){done(false);return;}
            advertising.Show(Guid.NewGuid().ToString("N"),config.gameId,"level_complete","interstitial",(state,error)=>done(state=="completed"));
        }
        public void ShowRewarded(string placement,Action<bool> done)
        {
            if(placement=="daily_reward")placement="daily_gift";
            if(!RewardedReady||game==null){done(false);return;}
            var current=game;var progress=current.Progress;string profile=progress.profileId;bool settled=false;bool paused=current.Paused;
            current.SetPaused(true);
            advertising.Show(Guid.NewGuid().ToString("N"),config.gameId,placement,"rewarded",(state,error)=>
            {
                if(settled)return;settled=true;
                bool active=current!=null&&current==GameController.Instance&&current.Progress==progress&&progress.profileId==profile;
                if(active)current.SetPaused(paused);
                if(active&&(state=="completed"||state=="cancelled"))AdClosed?.Invoke("rewarded");
                done(active&&state=="completed");
            });
        }
        void LateUpdate()
        {
            if(game==null||game.Hud==null||advertising==null)return;
            bool show=!background&&advertising.RequestsAllowed&&!advertising.IsFullscreenShowing&&!game.Hud.HasDialog&&game.BannerPauseSeconds==0;
            bool visible=show&&((IHostBannerDriver)advertising).IsReady;
            if(visible!=bannerVisible){bannerVisible=visible;advertising.SetBannerVisible(visible);}
            float inset=0;
            if(visible)
            {
                if(advertising.TryGetGeometry(out var screen,out var safe,out var rect,out float density))inset=rect.yMax;
                else inset=MaxSdkUtils.GetAdaptiveBannerHeight()*MaxSdkUtils.GetScreenDensity()+Screen.safeArea.yMin;
            }
            game.Hud.SetBannerInset(inset);
        }
        void OnApplicationPause(bool paused){background=paused;if(paused)advertising?.SetBannerVisible(false);bannerVisible=false;}
        void OnDestroy()
        {
            if(game!=null){game.SetCommerceProvider(null);game.Hud?.SetBannerInset(0);}
            if(instance==this)instance=null;
        }
    }
}

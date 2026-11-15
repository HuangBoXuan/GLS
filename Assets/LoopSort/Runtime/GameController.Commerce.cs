using System;
namespace LoopSort0
{
    public sealed partial class GameController
    {
        IPeripheralCommerceProvider commerceProvider;
        int commerceGeneration;
        public bool CommerceBusy {get;private set;}
        public bool RewardedAdsConfigured=>commerceProvider!=null&&commerceProvider.RewardedReady;
        public string AdvertisingStatus=>RewardedAdsConfigured?"广告已准备好":(commerceProvider as IPeripheralAdFeatures)?.AdStatus??"广告准备中，请稍后重试";
        public int BannerPauseSeconds=>Progress==null?0:(int)Math.Min(900,Math.Max(0,Math.Ceiling(Progress.bannerPausedUntilUtc-UtcSeconds)));
        public string BannerPauseLabel=>BannerPauseSeconds>0?TimeSpan.FromSeconds(BannerPauseSeconds).ToString(@"mm\:ss"):"15 分钟";
        public bool PremiumPaymentsConfigured
        {
            get {LoadServiceConfig();return commerceProvider!=null&&commerceProvider.PurchasesReady&&!string.IsNullOrEmpty(peripheralService.premiumPassProductId);}
        }
        public string PremiumPassPrice=>PremiumPaymentsConfigured?commerceProvider.PriceFor(peripheralService.premiumPassProductId):"";
        public void SetCommerceProvider(IPeripheralCommerceProvider provider)
        {
            if(commerceProvider is IPeripheralAdLifecycle oldEvents)oldEvents.AdClosed-=RewardedAdClosed;
            var present=adPendingPresentation;adPendingPresentation=null;
            commerceGeneration++;CommerceBusy=false;commerceProvider=provider;Hud?.MarkSkinDirty();
            if(provider is IPeripheralAdLifecycle events)events.AdClosed+=RewardedAdClosed;
            present?.Invoke();RefreshMusic();
        }
        static bool ValidBooster(string id)=>id=="undo"||id=="shuffle"||id=="belt"||id=="truck";
        public bool CanReviveWithAd=>State!=null&&State.Status==Core.RunStatus.Failed&&Hud!=null&&!Hud.BoosterPresentationBusy&&!CommerceBusy;
        public void ShowFailurePage()
        {
            if(CommerceBusy||adSettlementStarted||State==null||State.Status!=Core.RunStatus.Failed)return;
            LuaAction("lost");SetPaused(true);PresentSettlement(false);
        }
        public void ReviveWithAd()
        {
            if(!CanReviveWithAd)return;
            int attempt=run;var state=State;
            RunRewarded("revive",()=>
            {
                if(run!=attempt||State!=state||State.Status!=Core.RunStatus.Failed)return;
                Hud.Close();
                Hud.PlayBoosterUse("belt",()=>
                {
                    if(State!=state||!State.Revive())return;
                    PreserveBoosterHistory();resultShown=false;SetPaused(false);Hud.MarkSkinDirty();
                    RecordActivity("revive");Hud.Toast("已复活，传送带位置 +1");
                });
            });
        }
        public bool CanSkipLevel
        {
            get
            {
                if(IsDailyRun||Level==null||Catalog==null)return false;
                foreach(var level in Catalog.levels)if(level.id>Level.id)return true;
                return false;
            }
        }
        public void SkipLevelWithAd()
        {
            if(!CanSkipLevel)return;
            int attempt=run;var level=Level;
            RunRewarded("skip_level",()=>
            {
                if(run!=attempt||Level!=level)return;
                LuaAction("skip");
            });
        }
        public void OfferBooster(string id)
        {
            if(!ValidBooster(id)||CommerceBusy)return;
            SetPaused(true);HotUpdate.LuaUi.Render("booster_reward",Hud,id);
        }
        public void ClaimBoosterFromAd(string id)
        {
            if(!ValidBooster(id))return;
            RunRewarded("booster_"+id,()=>
            {
                switch(id){case "undo":Progress.undo++;break;case "shuffle":Progress.shuffle++;break;case "belt":Progress.belt++;break;case "truck":Progress.truck++;break;}
                SaveProgress();Hud.Close();SetPaused(Hud.IsHome||State.Status!=Core.RunStatus.Playing);Hud.MarkSkinDirty();Hud.Toast("已获得道具 ×1");
            });
        }
        public void ClaimBannerPause()
        {
            if(BannerPauseSeconds>0||Progress==null)return;
            RunRewarded("ad_break",()=>
            {
                Progress.bannerPausedUntilUtc=UtcSeconds+900;SaveProgress();
                Hud.Close();SetPaused(Hud.IsHome||State.Status!=Core.RunStatus.Playing);Hud.Toast("横幅广告已暂停 15 分钟");
            });
        }
        public void AdvertisingPrivacy()
        {
            if(!((commerceProvider as IPeripheralAdFeatures)?.ShowPrivacyOptions(ok=>{Hud?.MarkSkinDirty();if(!ok)Hud?.Toast("当前无需显示隐私选项，或服务暂不可用");})??false))Hud.Toast("当前无需显示隐私选项，或服务暂不可用");
        }
        public void ContinueWithAdvertising(int level)
        {
            if(!CommerceBusy)StartMainLevel(level);
        }
        void RunRewarded(string placement,Action granted)
        {
            if(CommerceBusy)return;
            if(!RewardedAdsConfigured)
            {
                (commerceProvider as IPeripheralAdFeatures)?.RetryAdvertising();Hud.Toast(AdvertisingStatus);return;
            }
            CommerceBusy=true;RefreshMusic();int generation=commerceGeneration;bool settled=false;var progress=Progress;string profile=Progress.profileId;
            try
            {
                commerceProvider.ShowRewarded(placement,success=>
                {
                    if(settled||this==null||generation!=commerceGeneration)return;
                    if(Progress!=progress||Progress.profileId!=profile){settled=true;CommerceBusy=false;RefreshMusic();return;}
                    settled=true;CommerceBusy=false;RefreshMusic();
                    if(success)
                    {
                        if(!(commerceProvider is IPeripheralAdLifecycle))RewardedAdClosed("rewarded");
                        if(!IsDailyRun&&(placement=="revive"||placement.StartsWith("booster_",StringComparison.Ordinal)))
                        {HotUpdate.LuaGameRuntime.Call("game.ad_cadence","gameplay_rewarded",Progress,Level.id);SaveProgress();}
                        granted();
                    }
                    else Hud.Toast("广告未完成，没有领取奖励");
                    Hud.MarkSkinDirty();
                });
            }
            catch(Exception){CommerceBusy=false;RefreshMusic();Hud.Toast("广告服务暂不可用");}
        }
        public void ClaimDoubledDailyReward()
        {
            if(Progress.lastDailyRewardDay==TodayKey)return;
            RunRewarded("daily_reward",()=>
            {
                LuaAction("daily_reward_video");
            });
        }
        public void PurchasePremiumPass()
        {
            if(CommerceBusy)return;
            if(!PremiumPaymentsConfigured){Hud.Toast("支付服务尚未配置，免费轨道可正常领取");return;}
            string cycle=Convert.ToString(LuaAction("pass_identity"));
            CommerceBusy=true;RefreshMusic();int generation=commerceGeneration;bool settled=false;
            try
            {
                commerceProvider.Purchase(peripheralService.premiumPassProductId,transaction=>
                {
                    if(settled||this==null||generation!=commerceGeneration)return;settled=true;CommerceBusy=false;RefreshMusic();
                    if(!string.IsNullOrEmpty(transaction))LuaAction("premium_purchased",transaction,cycle);
                    else Hud.Toast("购买未完成，没有解锁高级轨道");
                    if(Hud.HasDialog&&Hud.DialogTitle=="通行证")Hud.PeripheralPage("pass");
                });
            }
            catch(Exception){CommerceBusy=false;RefreshMusic();Hud.Toast("支付服务暂不可用");}
        }
    }
}

using System;
using LoopSort0.Core;
using LoopSort0.HotUpdate;
namespace LoopSort0
{
    public sealed partial class GameController
    {
        PlayerProgress adAttemptProgress;string adAttemptProfile;
        bool adAttemptEligible,adRoundRecorded,adSettlementStarted,adApplicationPaused,adEntryPending,adAttemptNewMechanic;
        bool adWindowFocused=true;Action adPendingPresentation;
        bool AdAccountCurrent=>Progress==adAttemptProgress&&Progress!=null&&Progress.profileId==adAttemptProfile;
        void BeginAdAttempt()
        {
            adAttemptProgress=Progress;adAttemptProfile=Progress.profileId;
            LuaGameRuntime.Call("game.ad_cadence","normalize",Progress,Progress.completedLevels.Count);
            adAttemptEligible=!IsDailyRun;adRoundRecorded=adSettlementStarted=adAttemptNewMechanic=false;
            adEntryPending=Convert.ToBoolean(LuaGameRuntime.Call("game.ad_cadence","begin",Progress,IsDailyRun?-1:Level.id));
            adPendingPresentation=null;
        }
        internal void NoteMechanicDiscovery(){adAttemptNewMechanic=true;}
        void TickAdCadence(){TryPresentEntryInterstitial();}
        void FlushAdGameplay(bool save){if(save&&AdAccountCurrent)SaveProgress();}
        void OnApplicationFocus(bool focused){adWindowFocused=focused;if(!focused)FlushAdGameplay(true);}
        void RewardedAdClosed(string format)
        {
            if(format!="rewarded"||Progress==null||!LuaGameRuntime.Ready)return;
            LuaGameRuntime.Call("game.ad_cadence","rewarded_closed",Progress,UtcSeconds);SaveProgress();
        }
        void RecordAdRound(bool won)
        {
            if(adRoundRecorded||!adAttemptEligible||!AdAccountCurrent)return;
            adRoundRecorded=true;
            LuaGameRuntime.Call("game.ad_cadence","round",Progress,Level.id,won,0,adAttemptNewMechanic);SaveProgress();
        }
        void PresentSettlement(bool won)
        {
            if(adSettlementStarted)return;
            adSettlementStarted=true;RecordAdRound(won);Hud.Result(won);
        }
        void TryPresentEntryInterstitial()
        {
            if(!adEntryPending||IsTestRun||IsDailyRun||!AdAccountCurrent||adApplicationPaused||!adWindowFocused||Paused||CommerceBusy||Hud==null||Hud.IsHome||Hud.HasDialog||Hud.BoosterPresentationBusy||State.Status!=RunStatus.Playing)return;
            // Consume before talking to the SDK. No fill/failure must not create
            // a late surprise or another request after reopening a menu.
            adEntryPending=false;SaveProgress();
            var ads=commerceProvider as IPeripheralSettlementAds;if(ads==null)return;
            int attempt=run,generation=commerceGeneration;var state=State;var progress=Progress;string profile=Progress.profileId;
            bool answered=false,wasPaused=Paused;CommerceBusy=true;SetPaused(true);
            Action resume=()=>
            {
                if(this!=null&&run==attempt&&State==state&&Progress==progress&&Progress.profileId==profile)
                    SetPaused(wasPaused||Hud.IsHome||Hud.HasDialog);
            };
            adPendingPresentation=resume;
            Action<bool> terminal=shown=>
            {
                if(answered||this==null||generation!=commerceGeneration)return;
                answered=true;CommerceBusy=false;RefreshMusic();adPendingPresentation=null;
                if(run!=attempt||State!=state||Progress!=progress||Progress.profileId!=profile)return;
                if(shown){LuaGameRuntime.Call("game.ad_cadence","interstitial_closed",Progress,UtcSeconds);SaveProgress();}
                resume();
            };
            try{ads.ShowSettlementInterstitial(terminal);}catch(Exception){terminal(false);}
        }
    }
}

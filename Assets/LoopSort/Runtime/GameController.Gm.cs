using UnityEngine;
using LoopSort0.Core;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        public void GmUnlockAllLevels()
        {
            if(!(Application.isEditor||Debug.isDebugBuild)||Progress==null||Catalog==null)return;
            int last=1;foreach(var level in Catalog.levels)last=Mathf.Max(last,level.id);
            Progress.highestUnlocked=last;SaveProgress();Hud.MarkSkinDirty();Hud.Toast("已解锁所有关卡");
        }
        public void GmClearBoosters()
        {
            if(!(Application.isEditor||Debug.isDebugBuild)||Progress==null)return;
            if(CommerceBusy||Hud.BoosterPresentationBusy){Hud.Toast("请等待广告或道具动画结束");return;}
            SetShuffleSelection(false);
            Progress.undo=Progress.shuffle=Progress.belt=Progress.truck=0;
            SaveProgress();Hud.Refresh(this);Hud.Toast("撤回、打乱、扩容、加车已清空");
        }

        public void GmFinishLevel(bool won)
        {
            if(!(Application.isEditor||Debug.isDebugBuild)||State==null||Hud==null)return;
            if(CommerceBusy||Hud.BoosterPresentationBusy||IsEnteringLevel)
            {Hud.Toast("请等待关卡、广告或道具动画结束");return;}
            if(resultShown||State.Status!=RunStatus.Playing)
            {Hud.Toast("本关已结束，请重新开始后再测试");return;}
            SetShuffleSelection(false);LuaAction("drop_tutorials");
            SetPaused(true);Hud.ShowGameplay();
            State.Data.status=won?RunStatus.Won:RunStatus.Failed;
            State.Data.failReason=won?FailReason.None:FailReason.ConveyorFull;
            resultShown=true;adSettlementStarted=won;
            Hud.Refresh(this);
            if(won){LuaAction("won");Hud.Result(true);}
            // A failure is settled only after the revive offer is dismissed.
            // Successful rewarded revival can resume this same attempt.
            else HotUpdate.LuaUi.Render("revive_offer",Hud);
        }
    }
}

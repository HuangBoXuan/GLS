using System;
using System.Collections;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        ShuffleSelectionMask shuffleMask;
        internal void ShowShuffleSelection(bool show)
        {
            if(shuffleMask!=null){shuffleMask.gameObject.SetActive(false);Destroy(shuffleMask.gameObject);shuffleMask=null;}
            if(!show)return;
            var panel=Panel(Canvas.transform as RectTransform,"Shuffle selection",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            panel.SetAsLastSibling();panel.gameObject.AddComponent<CanvasRenderer>();shuffleMask=panel.gameObject.AddComponent<ShuffleSelectionMask>();shuffleMask.Initialize(currentGame??GameController.Instance);
        }
        internal void LockShuffleMotion()
        {
            boosterGame=currentGame??GameController.Instance;boosterState=boosterGame.State;boosterWasPaused=boosterGame.Paused;
            boosterLayer=Panel(Canvas.transform as RectTransform,"Shuffle motion input lock",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var shield=Image(boosterLayer,"Input shield",null,Vector2.zero,Vector2.one);shield.color=Color.clear;shield.raycastTarget=true;
            boosterGame.SetPaused(true);
        }
        Coroutine boosterRoutine;
        RectTransform boosterLayer,boosterPulse;
        Vector3 boosterPulseScale;
        GameController boosterGame;
        Core.GameState boosterState;
        bool boosterWasPaused;
        internal bool BoosterPresentationBusy=>boosterLayer!=null;

        // Keep this presentation API internal: Lua still validates and spends
        // once through the existing controller methods, without a new bridge API.
        internal void PlayBoosterUse(string id,Action apply,Vector3? worldTarget=null)
        {
            if(BoosterPresentationBusy)return;
            var game=currentGame!=null?currentGame:GameController.Instance;
            if(game==null||game.State==null){apply?.Invoke();return;}
            boosterGame=game;boosterState=game.State;boosterWasPaused=game.Paused;
            boosterLayer=Panel(Canvas.transform as RectTransform,"Booster use",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            boosterLayer.SetAsLastSibling();
            var shade=Image(boosterLayer,"Booster dim",null,Vector2.zero,Vector2.one);
            shade.color=Color.clear;shade.raycastTarget=true;
            var icon=Image(boosterLayer,"Flying "+id,GameAssets.Sprite(id),Vector2.one*.5f,Vector2.one*.5f);
            icon.preserveAspect=true;icon.raycastTarget=false;icon.rectTransform.sizeDelta=new Vector2(170,170);
            int index=Array.IndexOf(new[]{"undo","shuffle","belt","truck"},id);
            var source=index>=0&&index<boosters.Count?boosters[index].transform.Find("Icon"):null;
            Vector2 from=source!=null?(Vector2)boosterLayer.InverseTransformPoint(source.position):Vector2.zero;
            Vector2 to=Vector2.zero;
            if(id=="belt"&&capacity!=null)
            {
                boosterPulse=capacity.transform.parent as RectTransform;
                boosterPulseScale=boosterPulse.localScale;
                to=boosterLayer.InverseTransformPoint(boosterPulse.position);
            }
            else if(worldTarget.HasValue)
            {
                var screenPoint=game.Camera.WorldToScreenPoint(worldTarget.Value);
                if(screenPoint.z>0)RectTransformUtility.ScreenPointToLocalPointInRectangle(boosterLayer,screenPoint,Canvas.worldCamera,out to);
            }
            game.SetPaused(true);
            boosterRoutine=StartCoroutine(BoosterUseMotion(icon,shade,from,to,apply));
        }

        IEnumerator BoosterUseMotion(UnityEngine.UI.Image icon,UnityEngine.UI.Image shade,Vector2 from,Vector2 to,Action apply)
        {
            float time=0;
            while(time<1.12f)
            {
                float fade=Mathf.Clamp01(time/.12f)*Mathf.Clamp01((1.12f-time)/.15f);
                shade.color=new Color(.10f,.055f,.09f,.62f*fade);
                float scale;Vector2 position;
                if(time<.5f)
                {
                    float t=BoosterEase(time/.5f);
                    position=Vector2.Lerp(from,Vector2.zero,t);scale=1+.65f*t+.12f*Mathf.Sin(Mathf.PI*t);
                }
                else if(time<.62f){position=Vector2.zero;scale=1.65f;}
                else
                {
                    float t=BoosterEase((time-.62f)/.35f);
                    position=Vector2.Lerp(Vector2.zero,to,t)+Vector2.up*(Mathf.Sin(Mathf.PI*t)*55);
                    scale=1.65f-1.4f*t;
                }
                icon.rectTransform.anchoredPosition=position;icon.rectTransform.localScale=Vector3.one*scale;
                icon.color=new Color(1,1,1,Mathf.Clamp01((1.02f-time)/.05f));
                if(boosterPulse!=null)boosterPulse.localScale=boosterPulseScale*(1+.13f*Mathf.Sin(Mathf.PI*Mathf.Clamp01((time-.97f)/.15f)));
                yield return null;
                time+=Time.unscaledDeltaTime;
            }
            var game=boosterGame;var state=boosterState;
            boosterRoutine=null;CancelBoosterPresentation(true);
            // A level switch cancels the coroutine and discards its callback.
            if(game!=null&&ReferenceEquals(game.State,state))apply?.Invoke();
        }
        static float BoosterEase(float t)=>.5f-.5f*Mathf.Cos(Mathf.PI*Mathf.Clamp01(t));

        internal void CancelBoosterPresentation(bool restorePause)
        {
            if(boosterRoutine!=null){StopCoroutine(boosterRoutine);boosterRoutine=null;}
            if(boosterPulse!=null)boosterPulse.localScale=boosterPulseScale;
            boosterPulse=null;
            if(boosterLayer!=null){boosterLayer.gameObject.SetActive(false);Destroy(boosterLayer.gameObject);boosterLayer=null;}
            if(restorePause&&boosterGame!=null&&ReferenceEquals(boosterGame.State,boosterState))boosterGame.SetPaused(boosterWasPaused);
            boosterGame=null;boosterState=null;
        }
        void OnDisable(){ShowShuffleSelection(false);CancelBoosterPresentation(true);}
    }
}

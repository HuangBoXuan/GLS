using System;
using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        RectTransform tutorialFocus,tutorialCaption;
        readonly RectTransform[] tutorialShades=new RectTransform[4];
        Image tutorialPointer;
        Text tutorialText;
        int tutorialTarget=-1,tutorialGroup=-1;
        bool tutorialTargetDirty=true,tutorialGeometryValid;
        BoxCollider tutorialCollider;
        Rect tutorialArea,tutorialPixelRect;
        Matrix4x4 tutorialCarrierMatrix,tutorialViewMatrix,tutorialProjectionMatrix,tutorialFocusMatrix;
        Vector3 tutorialColliderCenter,tutorialColliderSize;
        Vector2 tutorialPointerOrigin;

        void BuildTutorialFocus()
        {
            tutorialFocus=Panel(root,"Tutorial focus",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            // Four raycastable panels leave a real input hole over the selected truck.
            // The controller also checks the target so other input paths cannot bypass it.
            for(int i=0;i<tutorialShades.Length;i++)
            {
                var shade=Image(tutorialFocus,"Blocked region "+i,null,Vector2.zero,Vector2.zero);
                shade.color=new Color(ArtTheme.Ink.r,ArtTheme.Ink.g,ArtTheme.Ink.b,.7f);
                shade.raycastTarget=true;tutorialShades[i]=shade.rectTransform;
            }
            tutorialCaption=Art(tutorialFocus,"Tutorial caption","paper",Vector2.zero,Vector2.zero).rectTransform;
            tutorialText=Label(tutorialCaption,"Instruction","",32,new Vector2(.05f,.08f),new Vector2(.95f,.92f));
            tutorialPointer=Image(tutorialFocus,"Tutorial hand",GameAssets.Sprite("tutorialHand"),Vector2.zero,Vector2.zero);
            tutorialPointer.preserveAspect=true;tutorialPointer.rectTransform.sizeDelta=new Vector2(115,115);
            tutorialPointer.rectTransform.pivot=new Vector2(.08f,.92f);
            tutorialFocus.gameObject.SetActive(false);
        }

        void UpdateTutorialPointer(bool contentChanged)
        {
            bool eligible=currentGame!=null&&!IsHome&&!HasDialog&&!currentGame.Paused&&!currentGame.IsEnteringLevel&&!currentGame.IsTestRun&&!currentGame.IsDailyRun&&
                currentGame.Level.id>=1&&currentGame.Level.id<=2&&currentGame.State.Status==Core.RunStatus.Playing&&
                currentGame.State.BeltCount==0&&currentGame.State.PendingTransfers==0;
            if(!eligible){SetTutorialVisible(false);tutorialTargetDirty=true;return;}
            if(contentChanged||tutorialTargetDirty)
            {
                int target=Convert.ToInt32(HotUpdate.LuaGameRuntime.Call("game.flow","tutorial_target",currentGame));
                // A replay can replace all truck objects while keeping the same index.
                tutorialTarget=target;tutorialCollider=null;tutorialGeometryValid=false;tutorialTargetDirty=false;
            }
            if(tutorialTarget<0||tutorialTarget>=currentGame.TruckViews.Count||currentGame.Camera==null)
            {SetTutorialVisible(false);return;}

            var carrier=currentGame.TruckViews[tutorialTarget];var camera=currentGame.Camera;
            if(tutorialCollider==null)tutorialCollider=carrier.GetComponent<BoxCollider>();
            if(tutorialCollider==null){SetTutorialVisible(false);return;}
            int group=(currentGame.State.Carriers[tutorialTarget].cargo.Count-1)/currentGame.State.GroupSize;
            Rect area=tutorialFocus.rect,pixelRect=camera.pixelRect;
            Matrix4x4 carrierMatrix=carrier.localToWorldMatrix,viewMatrix=camera.worldToCameraMatrix,
                projectionMatrix=camera.projectionMatrix,focusMatrix=tutorialFocus.localToWorldMatrix;
            bool geometryChanged=!tutorialGeometryValid||tutorialGroup!=group||tutorialArea!=area||tutorialPixelRect!=pixelRect||
                tutorialCarrierMatrix!=carrierMatrix||tutorialViewMatrix!=viewMatrix||tutorialProjectionMatrix!=projectionMatrix||
                tutorialFocusMatrix!=focusMatrix||tutorialColliderCenter!=tutorialCollider.center||tutorialColliderSize!=tutorialCollider.size;
            if(geometryChanged)
            {
                if(!TryGetTutorialHole(carrier,tutorialCollider,out Rect hole))
                {tutorialGeometryValid=false;SetTutorialVisible(false);return;}
                SetTutorialRect(tutorialShades[0],Rect.MinMaxRect(area.xMin,area.yMin,hole.xMin,area.yMax));
                SetTutorialRect(tutorialShades[1],Rect.MinMaxRect(hole.xMax,area.yMin,area.xMax,area.yMax));
                SetTutorialRect(tutorialShades[2],Rect.MinMaxRect(hole.xMin,area.yMin,hole.xMax,hole.yMin));
                SetTutorialRect(tutorialShades[3],Rect.MinMaxRect(hole.xMin,hole.yMax,hole.xMax,area.yMax));

                const float margin=24,height=150,gap=32;
                float width=Mathf.Min(850,area.width*.88f);
                float x=Mathf.Clamp(hole.center.x-width*.5f,area.xMin+margin,area.xMax-margin-width);
                float above=area.yMax-hole.yMax,below=hole.yMin-area.yMin;
                float y=above>=height+gap+margin||above>=below?hole.yMax+gap:hole.yMin-gap-height;
                y=Mathf.Clamp(y,area.yMin+margin,area.yMax-margin-height);
                SetTutorialRect(tutorialCaption,new Rect(x,y,width,height));
                Vector3 point=camera.WorldToScreenPoint(carrier.TransformPoint(new Vector3(0,2.55f,1.29f-group)));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorialFocus,point,Canvas.worldCamera,out Vector2 local);
                local.x=Mathf.Clamp(local.x,hole.xMin+8,hole.xMax-8);local.y=Mathf.Clamp(local.y,hole.yMin+8,hole.yMax-8);
                tutorialPointerOrigin=local-area.min;
                tutorialArea=area;tutorialPixelRect=pixelRect;tutorialGroup=group;tutorialCarrierMatrix=carrierMatrix;
                tutorialViewMatrix=viewMatrix;tutorialProjectionMatrix=projectionMatrix;tutorialFocusMatrix=focusMatrix;
                tutorialColliderCenter=tutorialCollider.center;tutorialColliderSize=tutorialCollider.size;tutorialGeometryValid=true;
            }
            SetTutorialVisible(true);
            if(tutorialText.text!=instruction.text)tutorialText.text=instruction.text;
            // Only the hand animates every frame; the static mask and caption keep their geometry.
            tutorialPointer.rectTransform.anchoredPosition=tutorialPointerOrigin+new Vector2(12,-12)*(1+.3f*Mathf.Sin(Time.time*5));
            tutorialPointer.rectTransform.localScale=Vector3.one*(.95f+.05f*Mathf.Cos(Time.time*5));
        }

        void SetTutorialVisible(bool visible)
        {
            if(tutorialFocus.gameObject.activeSelf!=visible)tutorialFocus.gameObject.SetActive(visible);
            // Only the framed tutorial caption is visible. The legacy label is
            // a text source and must not reappear when the tutorial is hidden.
            if(instruction.gameObject.activeSelf)instruction.gameObject.SetActive(false);
        }

        bool TryGetTutorialHole(Transform carrier,BoxCollider collider,out Rect hole)
        {
            hole=default;
            if(collider==null||tutorialFocus.rect.width<=0||tutorialFocus.rect.height<=0)return false;
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<8;i++)
            {
                Vector3 corner=collider.center+Vector3.Scale(collider.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 point=currentGame.Camera.WorldToScreenPoint(carrier.TransformPoint(corner));
                if(point.z<=0)return false;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(tutorialFocus,point,Canvas.worldCamera,out Vector2 local);
                min=Vector2.Min(min,local);max=Vector2.Max(max,local);
            }
            Rect area=tutorialFocus.rect;
            min=Vector2.Max(area.min,min-Vector2.one*14);max=Vector2.Min(area.max,max+Vector2.one*14);
            if(max.x-min.x<16||max.y-min.y<16)return false;
            hole=Rect.MinMaxRect(min.x,min.y,max.x,max.y);return true;
        }

        void SetTutorialRect(RectTransform target,Rect rect)
        {
            target.anchorMin=target.anchorMax=Vector2.zero;target.pivot=Vector2.zero;
            target.anchoredPosition=rect.min-tutorialFocus.rect.min;
            target.sizeDelta=new Vector2(Mathf.Max(0,rect.width),Mathf.Max(0,rect.height));
        }
    }
}

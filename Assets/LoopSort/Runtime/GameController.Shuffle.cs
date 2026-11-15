using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace LoopSort0
{
    public sealed partial class GameController
    {
        internal bool CanShuffleCarrier(int index)=>State!=null&&index>=0&&index<State.Carriers.Count&&
            !VisualTransferBlocked(index)&&!featureViews[index].BlocksInteraction&&
            System.Convert.ToBoolean(HotUpdate.LuaGameRuntime.Call("game.rules","can_shuffle_data",State.Data,index));
        internal void SelectShuffleAt(Vector2 screenPoint)
        {
            if(!SelectingShuffle)return;
            var ray=Camera.ScreenPointToRay(screenPoint);RaycastHit hit;
            if(Physics.Raycast(ray,out hit,200))
            {
                var target=hit.collider.GetComponentInParent<CarrierClickTarget>();
                if(target!=null&&CanShuffleCarrier(target.Index)){Shuffle(target.Index);return;}
            }
            SetShuffleSelection(false);Hud.Toast("");
        }
        sealed class ShufflePose {public Transform target;public Vector3 from,to,scale;public Quaternion rotation;public float side;}
        IEnumerator ShuffleMotion(int carrier,Dictionary<int,int> oldSlots)
        {
            var display=cargoDisplays[carrier];var data=State.Carriers[carrier];var root=carriers[carrier];var poses=new List<ShufflePose>();
            for(int group=0;group<display.groups.Length;group++)
            {
                var go=display.groups[group];int slot=group*State.GroupSize;
                if(!go.activeSelf||slot>=data.cargo.Count)continue;int old;
                if(!oldSlots.TryGetValue(data.cargo[slot],out old))continue;
                var t=go.transform;poses.Add(new ShufflePose {target=t,to=t.position,from=root.TransformPoint(display.frontCenter+Vector3.back*(old/State.GroupSize)),rotation=t.rotation,scale=t.localScale,side=group%2==0?1:-1});
            }
            foreach(int id in data.cargo)
            {
                var view=pieces[id];int old;if(!view.gameObject.activeSelf||!oldSlots.TryGetValue(id,out old))continue;
                var t=view.transform;poses.Add(new ShufflePose {target=t,to=t.position,from=CargoPosition(carrier,old),rotation=t.rotation,scale=t.localScale,side=id%2==0?1:-1});
            }
            Hud.LockShuffleMotion();
            float elapsed=0;const float duration=.8f;
            while(elapsed<duration)
            {
                float t=Mathf.Clamp01(elapsed/duration),ease=t*t*(3-2*t),lift=Mathf.Sin(Mathf.PI*t);
                foreach(var pose in poses)if(pose.target!=null)
                {
                    pose.target.SetPositionAndRotation(Vector3.Lerp(pose.from,pose.to,ease)+Vector3.up*(lift*.65f)+root.right*(Mathf.Sin(t*Mathf.PI*2)*.25f*pose.side),pose.rotation*Quaternion.AngleAxis(360*ease,Vector3.up));
                    pose.target.localScale=pose.scale*(1-.1f*lift);
                }
                yield return null;elapsed+=Time.unscaledDeltaTime;
            }
            foreach(var pose in poses)if(pose.target!=null){pose.target.SetPositionAndRotation(pose.to,pose.rotation);pose.target.localScale=pose.scale;}
            Hud.CancelBoosterPresentation(true);
        }
    }
}

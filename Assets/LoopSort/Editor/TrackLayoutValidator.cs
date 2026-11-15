using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LoopSort0.Core;
using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    // Checks rendered placements against authored APK poses, without changing
    // the layout to hide intersections. No validator is included in builds.
    public static class TrackLayoutValidator
    {
        const string Output="Exports/Validation/NativeLayout.json";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public sealed class Entry
        {
            public int level,added,carts;public float maximumPositionError,maximumYawError,maximumScaleError;
            public float cameraSize;public string[] errors;public string[] viewportWarnings;
        }
        [Serializable] public sealed class Report
        {
            public bool running;public int totalLevels,levelsDone,checkedPlacements,checkedCarts;
            public string measurement="Runtime cart roots, yaw and scale compared with authored APK mouth coordinates minus rotated native pivot. Includes sequential spare carts; geometry checks do not constitute a playthrough.";
            public List<Entry> entries=new List<Entry>();public string error;
        }
        static Report report;static int spare,stage,originalLevel;static bool originalTest;
        static string originalProgress;static GameController game;
        [MenuItem("Tools/LoopSort/Validate all APK level layouts")]
        public static void Queue()
        {
            if(report!=null&&report.running)return;
            game=GameController.Instance;
            if(game==null||game.State==null)throw new InvalidOperationException("Start the game before validating layouts.");
            originalLevel=game.Level.id;originalTest=game.IsTestRun;originalProgress=JsonUtility.ToJson(game.Progress);
            game.IsTestRun=true;stage=0;spare=0;report=new Report {running=true,totalLevels=game.Catalog.levels.Length};
            Save();EditorApplication.update+=Tick;
        }
        [MenuItem("Tools/LoopSort/Stop APK layout validation")]
        public static void Stop(){if(report!=null&&report.running){report.error="Stopped";Finish();}}
        static object Field(string name)=>typeof(GameController).GetField(name,Private).GetValue(game);
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(game==null||!EditorApplication.isPlaying){report.error="Play mode ended";Finish();return;}
            if(report.levelsDone>=report.totalLevels){Finish();return;}
            var level=game.Catalog.levels[report.levelsDone];
            try
            {
                if(stage==0){game.LoadLevel(level.id);Snap();Record(0);stage=1;spare=0;}
                else if(spare<(level.spares?.Length??0))
                {
                    var data=level.spares[spare++];
                    if(!game.IsCarrierPlaced(data.id))typeof(GameController).GetMethod("PlaceSpareTruck",Private).Invoke(game,new object[]{data});
                    Snap();Record(spare);
                }
                else{report.levelsDone++;stage=0;if(report.levelsDone%25==0)Save();}
            }
            catch(Exception e)
            {
                report.entries.Add(new Entry {level=level.id,added=spare,errors=new[]{(e.InnerException??e).ToString()}});
                report.levelsDone++;stage=0;Save();
            }
        }
        static void Snap()
        {
            game.StopAllCoroutines();game.Hud.Close();game.SetPaused(true);
            var carts=(List<Transform>)Field("carriers");var origins=(List<Vector3>)Field("carrierOrigins");var layouts=(List<CarrierData>)Field("placements");
            for(int i=0;i<carts.Count;i++){carts[i].SetPositionAndRotation(origins[i],Quaternion.Euler(0,layouts[i].yaw,0));carts[i].localScale=Vector3.one;carts[i].GetComponent<CarrierFeatureView>().ResetAnimation();}
            foreach(var group in (List<CarrierGroupFeatureView>)Field("groupFeatures"))group.Refresh(game.State,true);
            var track=(TrackGeometry)Field("track");typeof(TrackGeometry).GetMethod("SetEntranceClip",Private).Invoke(track,new object[]{1f});typeof(TrackGeometry).GetMethod("SetEntranceDetails",Private).Invoke(track,new object[]{.6f});
            typeof(GameController).GetMethod("RevealEntranceMechanisms",Private).Invoke(game,new object[]{true});
        }
        static void Record(int added)
        {
            var entry=new Entry {level=game.Level.id,added=added,cameraSize=game.Camera.orthographicSize};var errors=new List<string>();
            var carts=(List<Transform>)Field("carriers");var layouts=(List<CarrierData>)Field("placements");var pivot=(Vector3)Field("sourcePivot");entry.carts=carts.Count;
            foreach(var pair in carts.Select((cart,i)=>new{cart,layout=layouts[i]}))
            {
                var d=pair.layout;var yaw=Quaternion.Euler(0,d.yaw,0);var expected=new Vector3(d.x,d.y,d.z)-yaw*pivot;
                float distance=Vector3.Distance(pair.cart.position,expected),angle=Quaternion.Angle(pair.cart.rotation,yaw),scale=Vector3.Distance(pair.cart.localScale,Vector3.one);
                entry.maximumPositionError=Mathf.Max(entry.maximumPositionError,distance);entry.maximumYawError=Mathf.Max(entry.maximumYawError,angle);entry.maximumScaleError=Mathf.Max(entry.maximumScaleError,scale);
                if(distance>.0001f||angle>.05f||scale>.0001f)errors.Add(d.id+": authored pose changed");
            }
            if(float.IsNaN(entry.cameraSize)||float.IsInfinity(entry.cameraSize)||entry.cameraSize<=0)errors.Add("Invalid camera framing");
            var warnings=new List<string>();float aspect=game.Camera.aspect;
            foreach(float testAspect in new[]{900f/1600f,750f/1650f})
            {
                game.Camera.aspect=testAspect;game.Reframe();
                foreach(var cart in carts)
                {
                    Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
                    foreach(var renderer in cart.GetComponentsInChildren<MeshRenderer>())
                    {
                        if(!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name.Contains("SourceHidden"))continue;
                        var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
                        for(int i=0;i<8;i++)
                        {
                            var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                            Vector2 v=game.Camera.WorldToViewportPoint(filter.transform.TransformPoint(p));min=Vector2.Min(min,v);max=Vector2.Max(max,v);
                        }
                    }
                    if(min.x<-.01f||max.x>1.01f||min.y<.12f||max.y>.94f)warnings.Add(cart.name+" aspect "+testAspect+" min "+min+" max "+max);
                }
            }
            game.Camera.aspect=aspect;game.Reframe();entry.viewportWarnings=warnings.ToArray();
            entry.errors=errors.ToArray();report.entries.Add(entry);report.checkedPlacements++;report.checkedCarts+=entry.carts;
        }
        static void Save(){Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,JsonUtility.ToJson(report,true));}
        static void Finish()
        {
            EditorApplication.update-=Tick;report.running=false;Save();
            if(game!=null&&EditorApplication.isPlaying)
            {
                game.IsTestRun=true;game.LoadLevel(originalLevel);game.Hud.Close();game.SetPaused(false);
                JsonUtility.FromJsonOverwrite(originalProgress,game.Progress);game.IsTestRun=originalTest;
            }
            Debug.Log("APK layout audit finished: "+report.levelsDone+" levels; "+report.checkedPlacements+" layouts; "+Output);
        }
    }
}

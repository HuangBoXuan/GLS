using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LoopSort0.Core;

namespace LoopSort0.Editor
{
    public static class GameSetup
    {
        [MenuItem("LoopSort/Create gameplay scene")]
        public static void CreateScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before creating the scene");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
            new GameObject("LoopSort0",typeof(GameController));
            Directory.CreateDirectory("Assets/LoopSort/Scenes");
            EditorSceneManager.SaveScene(scene,"Assets/LoopSort/Scenes/Gameplay.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/LoopSort/Scenes/Gameplay.unity",true)};
            PlayerSettings.productName="Loop Sort";PlayerSettings.companyName="LoopSort0";
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.runInBackground=true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.loopsort0.prototype");
            AssetDatabase.SaveAssets();
            Debug.Log("LoopSort0 gameplay scene created from new implementation and APK 4.6.0 reference data.");
        }
        public static string AuditCatalog()
        {
            var catalog=GameAssets.Data<LevelCatalog>("main");int supported=0,totalPieces=0;
            foreach(var level in catalog.levels)
            {
                var state=new GameState(level,allowBasicRulesFallback:true);state.AssertConservation();if(level.IsSupported)supported++;totalPieces+=state.TotalPieces;
                if(level.points.Length<3)throw new Exception("Missing conveyor points in "+level.id);
                if(state.TotalPieces!=level.PieceCount)throw new Exception("Cargo count differs in "+level.id);
            }
            return $"All {catalog.levels.Length} main levels selectable; {supported} use implemented mechanics and {catalog.levels.Length-supported} use explicit basic-rule fallback; {totalPieces} cube identities validated.";
        }
        public static string Capture(string path,int width=540,int height=960)
        {
            var game=GameController.Instance;if(game==null)throw new InvalidOperationException("Game is not running");
            var camera=game.Camera;var previous=camera.targetTexture;float aspect=camera.aspect;
            var rt=new RenderTexture(width,height,24);rt.Create();var old=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.aspect=width/(float)height;game.Reframe();
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;game.Reframe();RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            return path;
        }
        public static string InspectRun()
        {
            var game=GameController.Instance;if(game==null)return "not running";
            return Newtonsoft.Json.JsonConvert.SerializeObject(new{level=game.Level.id,status=game.State.Status.ToString(),
                cubes=game.State.TotalPieces,belt=game.State.BeltCount,transfers=game.State.PendingTransfers,packed=game.State.PackedCount,
                carriers=game.State.Carriers.Select(c=>new{c.id,count=c.cargo.Count,c.packed,c.releasing}),
                undo=game.UndoDepth,paused=game.Paused});
        }
    }
}

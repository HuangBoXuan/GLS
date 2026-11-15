using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace LoopSort0.Editor
{
    public static class DailySigninPreview
    {
        public static void Check()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use an isolated batch project for sign-in verification");
            using(var lua=new LuaEnv())
            {
                int chunks=0;
                foreach(string path in Directory.GetFiles("Assets/StreamingAssets/LoopSort/lua","*.lua",SearchOption.AllDirectories))
                {using(var chunk=lua.LoadString(File.ReadAllText(path),"@"+path)){}chunks++;}
                lua.AddLoader((ref string module,ModuleLuaBuffer buffer)=>
                {
                    string path="Assets/StreamingAssets/LoopSort/lua/"+module.Replace('.','/')+".lua";
                    if(!File.Exists(path))return false;
                    buffer.Copy(File.ReadAllBytes(path));return true;
                });
                var result=lua.DoString(File.ReadAllText("Tools/Tests/Signin.lua"),"@Tools/Tests/Signin.lua");
                var ads=lua.DoString(File.ReadAllText("Tools/Tests/ProductAds.lua"),"@Tools/Tests/ProductAds.lua");
                var cadence=lua.DoString(File.ReadAllText("Tools/Tests/AdCadence.lua"),"@Tools/Tests/AdCadence.lua");
                Directory.CreateDirectory("Design/MechanismUI/DailySignin");
                File.WriteAllText("Design/MechanismUI/DailySignin/logic-validation.json",JsonConvert.SerializeObject(new{status="passed",luaSyntaxCount=chunks,result=result[0],productAds=ads[0],adCadence=cadence[0]},Formatting.Indented)+"\n");
            }
        }
        public static void Capture(GameController game,string directory,int width,int height)
        {
            var p=game.Progress;
            int oldCount=p.dailyRewardCount,undo=p.undo,belt=p.belt,shuffle=p.shuffle,truck=p.truck;
            string oldDay=p.lastDailyRewardDay;
            try
            {
                foreach(int count in new[]{0,3,6})
                {
                    p.dailyRewardCount=count;p.lastDailyRewardDay="";
                    game.Hud.PeripheralPage("daily_reward");
                    GameSetup.Capture(Path.Combine(directory,"day-"+(count+1)+".png"),width,height);
                    CheckLabels(game);
                }
                // Exercise the rendered claim button and its in-place state update.
                var dialog=game.Hud.DialogRoot;
                int uiNodes=dialog.GetComponentsInChildren<RectTransform>().Length;
                var claim=game.Hud.DialogRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Signin claim");
                claim.onClick.Invoke();
                if(game.Hud.DialogRoot!=dialog||dialog.GetComponentsInChildren<RectTransform>().Length!=uiNodes)
                    throw new Exception("Claim rebuilt the sign-in hierarchy instead of updating it");
                if(p.dailyRewardCount!=7||p.lastDailyRewardDay!=game.TodayKey||p.undo!=undo+2||p.belt!=belt+2||p.shuffle!=shuffle+2||p.truck!=truck+2)
                    throw new Exception("Rendered sign-in button did not grant day-seven rewards");
                GameSetup.Capture(Path.Combine(directory,"day-7-claimed.png"),width,height);
                var next=game.Hud.DialogRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Signin claim");
                next.onClick.Invoke();
                if(game.Hud.HasDialog)throw new Exception("Continue button did not close sign-in");
                File.WriteAllText(Path.Combine(directory,"ui-validation.json"),JsonConvert.SerializeObject(new{status="passed",width,height,claimButton="passed",continueButton="passed",inventory="four boosters +2",claimReusesHierarchy=true,uiNodes},Formatting.Indented)+"\n");
            }
            finally
            {
                p.dailyRewardCount=oldCount;p.lastDailyRewardDay=oldDay;p.undo=undo;p.belt=belt;p.shuffle=shuffle;p.truck=truck;
                game.SaveProgress();game.Hud.Close();
            }
        }
        static void CheckLabels(GameController game)
        {
            var labels=game.Hud.DialogRoot.GetComponentsInChildren<Text>();
            var days=labels.Where(t=>t.name=="Day").ToArray();
            var amounts=labels.Where(t=>t.name.StartsWith("Quantity ")).ToArray();
            if(days.Length!=7||amounts.Length!=20||days.Concat(amounts).Any(t=>t.cachedTextGenerator.vertexCount==0)||amounts.Any(t=>t.cachedTextGenerator.lineCount>1))
                throw new Exception("A sign-in day or reward quantity has no rendered text");
        }
    }
}

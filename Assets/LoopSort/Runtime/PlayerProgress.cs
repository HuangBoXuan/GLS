using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LoopSort0
{
    [Serializable] public sealed class PlayerProgress
    {
        const string Key="LoopSort0.Player.v1";
        public const int StartingCoins=0;
        public int version=1,currentLevel=1,highestUnlocked=1,coins=StartingCoins;
        public bool startingCoinsGranted;
        public int undo=2,shuffle=2,belt=2,truck=2;
        public bool sound=true,music=true,haptics=true;
        public string playerName="玩家";
        public string language="zh-CN";
        public int homeArea;
        public int peripheralVersion,areaId=1,avatarIndex,lives=5;
        public double nextLifeAt,unlimitedLifeUntil;
        public double bannerPausedUntilUtc;
        public int adCadenceVersion,adTotalWins,adRoundsSinceInterstitial,adFailureLevel=-1,adFailuresAtLevel;
        public double adGameplaySeconds,adGameplaySecondsSinceInterstitial,adLastInterstitialUtc,adLastRewardedClosedUtc;
        public string lastDailyRewardDay="",profileId="";
        public int dailyRewardCount;
        public List<string> dailyCompleted=new List<string>(),dailyFailed=new List<string>(),dailyClaimed=new List<string>(),boosterLessons=new List<string>();
        public List<int> learnedFeatures=new List<int>(),completedLevels=new List<int>(),skippedLevels=new List<int>();
        public Dictionary<string,string> luaData=new Dictionary<string,string>();
        public static PlayerProgress Load()
        {
            string record=PlayerPrefs.GetString(Key,"");PlayerProgress result=null;
            try {if(!string.IsNullOrEmpty(record))result=JsonConvert.DeserializeObject<PlayerProgress>(record);}
            catch(JsonException error)
            {
                PlayerPrefs.SetString(Key+".corrupt",record);Debug.LogWarning("Saved progress could not be read: "+error.Message);
            }
            result=result??new PlayerProgress();result.EnsureContainers();
            // Lua errors are allowed to propagate; they must never be mistaken for
            // corrupted player data and overwrite the previous progress record.
            HotUpdate.LuaGameRuntime.Call("game.flow","normalize_progress",result);
            result.Save();return result;
        }
        public void EnsureContainers()
        {
            language=UiLocalization.Normalize(language);
            if(learnedFeatures==null)learnedFeatures=new List<int>();
            if(completedLevels==null)completedLevels=new List<int>();
            if(skippedLevels==null)skippedLevels=new List<int>();
            if(luaData==null)luaData=new Dictionary<string,string>();
            if(dailyCompleted==null)dailyCompleted=new List<string>();
            if(dailyFailed==null)dailyFailed=new List<string>();
            if(dailyClaimed==null)dailyClaimed=new List<string>();
            if(boosterLessons==null)boosterLessons=new List<string>();
            if(string.IsNullOrEmpty(profileId))profileId=Guid.NewGuid().ToString("N");
        }
        public static PlayerProgress ResetLocal(){PlayerPrefs.DeleteKey(Key);PlayerPrefs.DeleteKey(Key+".corrupt");return Load();}
        public bool GrantStartingCoins()=>Convert.ToBoolean(HotUpdate.LuaGameRuntime.Call("game.flow","grant_coins",this));
        public void Save(){PlayerPrefs.SetString(Key,JsonConvert.SerializeObject(this));PlayerPrefs.Save();}
    }
}

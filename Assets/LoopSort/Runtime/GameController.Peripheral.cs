using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LoopSort0.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        public bool IsDailyRun {get;private set;}
        public string DailyDate {get;private set;}="";
        public int LastGoldReward {get;private set;}
        public double UtcSeconds=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public string TodayKey=>DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        public string CurrentMonth=>DateTime.Today.ToString("yyyy-MM",CultureInfo.InvariantCulture);
        // Compatibility for the archived profile UI; the active game has no portrait picker.
        public int AvatarCount=>AvatarIds.Length;
        // Keep saved/server avatar identifiers compatible; no standalone portrait textures are loaded.
        static readonly string[] AvatarIds={"default","beachgirl","blackcurlyman","blackhairtallguy","blackhairwoman","blonde","brownhairman","tennisgirl","worker"};
        LevelCatalog dailyCatalog;
        bool peripheralResultApplied,peripheralResultWon;
        float peripheralTickAt;
        void OnPeripheralLevelLoaded()
        {
            peripheralResultApplied=false;LastGoldReward=0;
            if(PeripheralAccountConnected&&Progress.profileId!=serviceAccountId){Progress.profileId=serviceAccountId;SaveProgress();}
        }
        void TickPeripheral()
        {
            if(peripheralResultApplied&&!peripheralResultWon&&State.Status==RunStatus.Playing)peripheralResultApplied=false;
            if(Time.unscaledTime<peripheralTickAt)return;
            peripheralTickAt=Time.unscaledTime+1;
            LuaAction("peripheral_tick");
            HotUpdate.LuaGameRuntime.Call("ui.pages","refresh_peripheral",Hud,this);
            HotUpdate.LuaGameRuntime.Call("ui.pages","refresh_advertising",Hud);
        }
        public int CompletePeripheralRun(bool won)
        {
            if(peripheralResultApplied)return LastGoldReward;
            peripheralResultApplied=true;peripheralResultWon=won;
            LastGoldReward=Convert.ToInt32(LuaAction("peripheral_complete",won));return LastGoldReward;
        }
        public void RecordActivity(string key,int amount=1){if(bootComplete&&!IsTestRun)LuaAction("activity_event",key,amount);}
        void FlushPeripheralProgress()
        {
            if(Progress!=null&&!IsTestRun&&HotUpdate.LuaGameRuntime.Ready)LuaAction("peripheral_flush");
        }
        void OnApplicationPause(bool paused){adApplicationPaused=paused;RefreshMusic();if(paused){FlushAdGameplay(true);FlushPeripheralProgress();}}
        void OnApplicationQuit(){FlushAdGameplay(true);FlushPeripheralProgress();}
        public bool StartMainLevel(int id)
        {
            if(!Convert.ToBoolean(LuaAction("peripheral_can_start")))return false;
            if(!Catalog.levels.Any(l=>l.id==id))return false;
            LoadLevel(id);return true;
        }
        public void LeaveDailyForHome(){if(IsDailyRun)LoadLevel(Progress.currentLevel);}
        public bool StartDaily(string date)
        {
            if(!DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day)||day.Date>DateTime.Today||day.ToString("yyyy-MM")!=CurrentMonth)
            {Hud.Toast("只能挑战本月已经到来的日期");return false;}
            if(!Convert.ToBoolean(LuaAction("peripheral_can_start")))return false;
            if(dailyCatalog==null)dailyCatalog=GameAssets.Data<LevelCatalog>("daily-challenge");
            if(dailyCatalog?.levels==null||dailyCatalog.levels.Length==0){Hud.Toast("每日挑战关卡资源未加载");return false;}
            var order=DailyMonthOrder(dailyCatalog.levels.Length,day.Year*12+day.Month);
            IsDailyRun=true;DailyDate=date;LoadLevelData(dailyCatalog.levels[order[(day.Day-1)%order.Length]]);return true;
        }
        static int[] DailyMonthOrder(int count,int month)
        {
            var order=Enumerable.Range(0,count).ToArray();ulong state=unchecked((ulong)month);
            for(int i=count-1;i>0;i--)
            {
                ulong value;
                unchecked {state+=0x9E3779B97F4A7C15UL;value=state;value=(value^(value>>30))*0xBF58476D1CE4E5B9UL;value=(value^(value>>27))*0x94D049BB133111EBUL;value^=value>>31;}
                int other=(int)(value%(ulong)(i+1));int old=order[i];order[i]=order[other];order[other]=old;
            }
            return order;
        }
        public int CalendarDays(string month)=>DateTime.TryParseExact(month,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?DateTime.DaysInMonth(date.Year,date.Month):0;
        public int CalendarOffset(string month)=>DateTime.TryParseExact(month,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?((int)date.DayOfWeek+6)%7:0;
        public Sprite ProfileAvatar(int index)=>GameAssets.Sprite("avatar");

        [Serializable] sealed class ServiceConfig {public string baseUrl="",premiumPassProductId="";public int timeoutSeconds=15;}
        [Serializable] sealed class RankResponse {public RankEntry[] entries=Array.Empty<RankEntry>();}
        [Serializable] sealed class RankEntry {public int rank,level;public string playerName,profileId,avatar;}
        [Serializable] sealed class CloudEnvelope {public int schema=1;public string revision,profileId;public PlayerProgress progress;}
        ServiceConfig peripheralService;
        string serviceToken="",serviceAccountId="",cloudRevision="";
        int serviceGeneration;
        Coroutine peripheralRequest;
        UnityWebRequest peripheralWebRequest;
        PlayerProgress pendingCloudProgress;
        RankEntry[] ranking=Array.Empty<RankEntry>();
        public bool ServiceBusy {get;private set;}
        public string ServiceStatus {get;private set;}="";
        public bool CloudRestoreAvailable=>pendingCloudProgress!=null;
        public bool PeripheralAccountConnected=>!string.IsNullOrEmpty(serviceAccountId)&&!string.IsNullOrEmpty(serviceToken);
        public int LeaderboardCount=>ranking.Length;
        public bool ServicesConfigured
        {
            get {LoadServiceConfig();return Uri.TryCreate(peripheralService.baseUrl,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps;}
        }
        void LoadServiceConfig()
        {
            if(peripheralService!=null)return;
            try {peripheralService=GameAssets.Data<ServiceConfig>("peripheral-services");}catch(Exception){peripheralService=new ServiceConfig();}
            if(peripheralService==null)peripheralService=new ServiceConfig();
        }
        public void ConfigurePeripheralService(string httpsUrl,string bearerToken)
        {
            CancelPeripheralRequest();
            LoadServiceConfig();peripheralService.baseUrl=httpsUrl??"";serviceToken=bearerToken??"";
            serviceAccountId="";
            cloudRevision="";pendingCloudProgress=null;ranking=Array.Empty<RankEntry>();
        }
        // The host login SDK supplies the same account ID on every device.
        // Bearer credentials remain in memory and never enter PlayerProgress.
        public void ConfigurePeripheralIdentity(string accountId,string bearerToken)
        {
            CancelPeripheralRequest();
            serviceAccountId=accountId??"";serviceToken=bearerToken??"";
            cloudRevision="";pendingCloudProgress=null;ranking=Array.Empty<RankEntry>();
            if(!string.IsNullOrEmpty(serviceAccountId)&&Progress!=null)
            {
                Progress.profileId=serviceAccountId;SaveProgress();
            }
            ServiceStatus=PeripheralAccountConnected?"已连接账户":"尚未连接账户";
        }
        public void ResetLocalProgress()
        {
            CancelPeripheralRequest();pendingCloudProgress=null;cloudRevision="";
            Progress=PlayerProgress.ResetLocal();StartMainLevel(1);ShowHome();
        }
        void CancelPeripheralRequest()
        {
            serviceGeneration++;
            commerceGeneration++;CommerceBusy=false;RefreshMusic();
            var request=peripheralWebRequest;var routine=peripheralRequest;
            peripheralWebRequest=null;peripheralRequest=null;
            ServiceBusy=false;
            // StopCoroutine may dispose the iterator (and its using block).
            // Detach first so either disposal order leaves no stale busy state.
            try {if(request!=null)request.Abort();}catch(ObjectDisposedException){}
            try {if(routine!=null&&Hud!=null)Hud.StopCoroutine(routine);}
            finally {try {if(request!=null)request.Dispose();}catch(ObjectDisposedException){}}
        }
        public string LeaderboardRow(int index)
        {
            if(index<0||index>=ranking.Length)return "";
            var row=ranking[index];return row.rank+".  "+row.playerName+"  ·  第 "+row.level+" 关";
        }
        bool BeginServiceRequest(bool accountRequired=false)
        {
            if(ServiceBusy)return false;
            if(!ServicesConfigured){ServiceStatus="尚未配置排行榜和云存档服务器";Hud.Toast(ServiceStatus);return false;}
            if(accountRequired&&!PeripheralAccountConnected){ServiceStatus="尚未连接账户，请先接入正式登录服务";Hud.Toast(ServiceStatus);return false;}
            ServiceBusy=true;ServiceStatus="正在连接服务器…";return true;
        }
        public void FetchLeaderboard(string scope)
        {
            if(scope!="weekly"&&scope!="country")scope="world";
            if(!BeginServiceRequest())return;
            peripheralRequest=Hud.StartCoroutine(PeripheralRequest("GET","leaderboard?scope="+scope+"&limit=30",null,body=>
            {
                var response=JsonConvert.DeserializeObject<RankResponse>(body);
                if(response?.entries==null)throw new FormatException("排行榜响应缺少 entries");
                ranking=response.entries.Where(e=>e!=null&&e.rank>0&&e.level>0).Take(30).ToArray();
                ServiceStatus=ranking.Length==0?"服务器暂无排行记录":"排行榜已更新";
            }));
        }
        public void SubmitLeaderboard()
        {
            if(!BeginServiceRequest(true))return;
            string body=JsonConvert.SerializeObject(new {profileId=Progress.profileId,playerName=Progress.playerName,avatar=AvatarIds[Mathf.Clamp(Progress.avatarIndex,0,AvatarIds.Length-1)],level=Progress.highestUnlocked,completed=Progress.completedLevels.Count});
            peripheralRequest=Hud.StartCoroutine(PeripheralRequest("POST","leaderboard/scores",body,_=>ServiceStatus="成绩已提交，排名由服务器计算"));
        }
        public void UploadCloudSave()
        {
            if(!BeginServiceRequest(true))return;
            var envelope=new CloudEnvelope {profileId=Progress.profileId,revision=cloudRevision,progress=Progress};
            peripheralRequest=Hud.StartCoroutine(PeripheralRequest("PUT","players/"+Uri.EscapeDataString(Progress.profileId)+"/save",JsonConvert.SerializeObject(envelope),body=>
            {
                var response=JsonConvert.DeserializeObject<CloudEnvelope>(body);cloudRevision=response?.revision??cloudRevision;ServiceStatus="云存档已保存";
            }));
        }
        public void DownloadCloudSave()
        {
            if(!BeginServiceRequest(true))return;pendingCloudProgress=null;
            peripheralRequest=Hud.StartCoroutine(PeripheralRequest("GET","players/"+Uri.EscapeDataString(Progress.profileId)+"/save",null,body=>
            {
                var response=JsonConvert.DeserializeObject<CloudEnvelope>(body);
                if(response==null||response.schema!=1||response.progress==null||response.profileId!=Progress.profileId)throw new FormatException("云存档版本或账户不匹配");
                response.progress.EnsureContainers();response.progress.profileId=Progress.profileId;
                pendingCloudProgress=response.progress;cloudRevision=response.revision;
                ServiceStatus="已找到云存档，确认后恢复；本地存档会先备份";
            }));
        }
        public void RestoreCloudSave()
        {
            if(pendingCloudProgress==null||!Hud.IsHome)return;
            PlayerPrefs.SetString("LoopSort0.Player.beforeCloud",JsonConvert.SerializeObject(Progress));
            Progress=pendingCloudProgress;pendingCloudProgress=null;
            HotUpdate.LuaGameRuntime.Call("game.flow","normalize_progress",Progress);
            Progress.Save();ServiceStatus="已恢复云存档，本地旧记录已备份";
            LoadLevel(Catalog.levels.Any(l=>l.id==Progress.currentLevel)?Progress.currentLevel:Catalog.levels[0].id);ShowHome();
        }
        void ReleasePeripheralAssets()
        {
            FlushPeripheralProgress();
            CancelPeripheralRequest();
        }
        IEnumerator PeripheralRequest(string method,string suffix,string body,Action<string> completed)
        {
            int generation=serviceGeneration;
            try
            {
                using(var request=new UnityWebRequest(peripheralService.baseUrl.TrimEnd('/')+"/"+suffix,method))
                {
                    peripheralWebRequest=request;
                    request.downloadHandler=new DownloadHandlerBuffer();request.timeout=Mathf.Clamp(peripheralService.timeoutSeconds,5,60);
                    request.SetRequestHeader("Accept","application/json");
                    if(!string.IsNullOrEmpty(serviceToken))request.SetRequestHeader("Authorization","Bearer "+serviceToken);
                    if(body!=null){request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.SetRequestHeader("Content-Type","application/json");}
                    if(method=="PUT")request.SetRequestHeader(string.IsNullOrEmpty(cloudRevision)?"If-None-Match":"If-Match",string.IsNullOrEmpty(cloudRevision)?"*":cloudRevision);
                    yield return request.SendWebRequest();
                    if(generation!=serviceGeneration)yield break;
                    try
                    {
                        if(request.result!=UnityWebRequest.Result.Success)ServiceStatus=request.responseCode==409||request.responseCode==412?"云存档已变化，请先下载再决定恢复或上传":"连接失败，请检查服务地址和网络（"+request.responseCode+"）";
                        else
                        {
                            string response=request.downloadHandler.text;
                            if(response.Length>4*1024*1024)throw new FormatException("Response too large");
                            completed(response);
                        }
                    }
                    catch(Exception){ServiceStatus="服务器返回的数据格式不正确，未应用任何存档";}
                }
            }
            finally
            {
                if(generation==serviceGeneration){ServiceBusy=false;peripheralRequest=null;peripheralWebRequest=null;}
            }
            if(generation==serviceGeneration&&Hud!=null)HotUpdate.LuaGameRuntime.Call("ui.pages","service_refresh",Hud,this);
        }
    }
}

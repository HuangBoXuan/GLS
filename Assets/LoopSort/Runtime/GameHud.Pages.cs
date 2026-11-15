using System;
using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        Text homeCoins;
        static int[] BoosterPrices=>new[]{Price("undo"),Price("shuffle"),Price("belt"),Price("truck")};
        static int Price(string id)=>Convert.ToInt32(HotUpdate.LuaGameRuntime.Call("game.flow","booster_price",id));
        public void Home(PlayerProgress progress,int playLevel)=>HotUpdate.LuaUi.Render("home",this,progress,playLevel);
        public void FeatureTutorial(MechanicGuide guide,Action next,bool discovery=true)
        {
            if(discovery)(currentGame??GameController.Instance)?.NoteMechanicDiscovery();
            HotUpdate.LuaUi.Render("tutorial",this,guide,next,discovery);
        }
        public void Mechanics()=>HotUpdate.LuaUi.Render("mechanics",this);
        public void Shop()=>HotUpdate.LuaUi.Render("shop",this);
        public void BuyBooster(int item)=>HotUpdate.LuaUi.Render("buy",this,item);
        public void Areas()=>HotUpdate.LuaUi.Render("areas",this);
        public void Ranking()=>HotUpdate.LuaUi.Render("ranking",this);
        public void Teams()=>HotUpdate.LuaUi.Render("teams",this);
        public void DailyPractice()=>HotUpdate.LuaUi.Render("daily",this);
        public void Profile()=>HotUpdate.LuaUi.Render("profile",this);
        public void OpenHome()
        {
            Close();homeCoins=null;homeLife=null;toast.text="";toastPlate.gameObject.SetActive(false);gameplay.gameObject.SetActive(false);home.gameObject.SetActive(true);
            PositionGmButton(true);
            foreach(Transform child in home){child.gameObject.SetActive(false);ReleaseCallbacks(child.gameObject);Destroy(child.gameObject);}skinDirty=true;
        }
        public void SetHomeCoinLabel(Text label){homeCoins=label;}
        Text homeLife;
        public Text HomeLifeLabel=>homeLife;
        public void SetHomeLifeLabel(Text label){homeLife=label;}
        public void PeripheralPage(string page,string argument="")=>HotUpdate.LuaUi.Render("peripheral_"+page,this,argument);
        public void MarkSkinDirty(){skinDirty=true;hudContentDirty=true;}
        public void HomeIllustration(RectTransform parent)
        {
            // Artwork fills the physical screen; buttons remain in the safe area.
            if(homeArtwork!=null){homeArtwork.gameObject.SetActive(true);return;}
            homeArtwork=Panel(transform as RectTransform,"Full screen home artwork",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            homeArtwork.SetAsFirstSibling();
            var plate=Panel(homeArtwork,"Home background",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<RawImage>();
            plate.texture=Resources.Load<Texture2D>("LoopSort/Theme/candy-factory-home");plate.raycastTarget=false;
            plate.uvRect=ArtTheme.BackgroundUv(plate.texture);
            GameLogo.Create(homeArtwork);
        }
        public ReferenceImage FillBackground(RectTransform parent,string name,string artwork)
        {
            // Backgrounds fill the viewport without cropping their edge artwork.
            return Art(parent,name,artwork,Vector2.zero,Vector2.one);
        }
        public void SetDialogClose(Action action)
        {
            dialogClose.onClick.RemoveAllListeners();dialogClose.onClick.AddListener(()=>action?.Invoke());
        }
        public int DailyIndex(int count)
        {
            int days=(int)(DateTime.Today-new DateTime(2026,1,1)).TotalDays;return (days%count+count)%count;
        }
        public string Trim(string text)=>text.Trim();
        public string TodayLabel()=>DateTime.Today.ToString(UiLocalization.Current=="zh-CN"?"yyyy 年 M 月 d 日":"dd/MM/yyyy");
        public void ReleaseLuaCallbacks()
        {
            if(HotUpdate.LuaGameRuntime.Ready)HotUpdate.LuaGameRuntime.Call("ui.pages","release_signin",this);
            ReleaseCallbacks(gameObject);currentGame=null;homeCoins=null;
        }
        static void ReleaseCallbacks(GameObject target)
        {
            foreach(var button in target.GetComponentsInChildren<Button>(true))button.onClick.RemoveAllListeners();
            foreach(var input in target.GetComponentsInChildren<InputField>(true)){input.onValueChanged.RemoveAllListeners();input.onEndEdit.RemoveAllListeners();}
        }
        public InputField Input(RectTransform parent,string placeholder,string value,Vector2 min,Vector2 max)
        {
            var art=Art(parent,"Input","paper",min,max);art.raycastTarget=true;
            var field=art.gameObject.AddComponent<InputField>();field.targetGraphic=art;
            var text=Label(art.rectTransform,"Value",value,35,new Vector2(.04f,.04f),new Vector2(.96f,.96f));
            if(text is LocalizedText localized){localized.TranslateSource=false;localized.RefreshLanguage();}
            text.alignByGeometry=false;text.resizeTextForBestFit=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
            var hint=Label(art.rectTransform,"Placeholder",placeholder,32,new Vector2(.04f,.04f),new Vector2(.96f,.96f));
            if(text.transform.parent.GetComponent<RectMask2D>()==null)text.transform.parent.gameObject.AddComponent<RectMask2D>();
            field.lineType=InputField.LineType.SingleLine;field.customCaretColor=true;field.caretColor=Color.black;field.caretWidth=3;
            field.textComponent=text;field.placeholder=hint;field.text=value;inputs.Add(field);return field;
        }
        void FitInputFields()
        {
            for(int i=inputs.Count-1;i>=0;i--)
            {
                var field=inputs[i];if(field==null){inputs.RemoveAt(i);continue;}
                if(!field.gameObject.activeInHierarchy)continue;
                var text=field.textComponent;float height=text.rectTransform.rect.height;
                if(height<=0||text.font==null)continue;
                float lineRatio=text.font.lineHeight/(float)Mathf.Max(1,text.font.fontSize);
                int size=Mathf.Clamp(Mathf.FloorToInt((height-2)/Mathf.Max(1,lineRatio)),12,44);
                if(text.fontSize==size)continue;
                text.fontSize=size;field.ForceLabelUpdate();
            }
        }
    }
}

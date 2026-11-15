using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LoopSort0
{
    public sealed partial class GameHud : MonoBehaviour
    {
        public Action OnPause, OnRetry, OnContinue, OnLevels, OnUndo, OnShuffle, OnBelt, OnTruck, OnCancel;
        public Action<int> OnLevel;
        public Action<string> OnToggle;
        public Action OnHome,OnPlay,OnHelp;
        public Action OnCheckUpdates;
        public Func<string,bool> OnBuyBooster;
        public Action<string> OnName;
        public Action<int> OnArea;
        public Canvas Canvas { get; private set; }
        RectTransform root, overlay, overlayBackground, modal,gameplay,home,homeArtwork,toastPlate;
        GameController currentGame;
        bool skinDirty;
        internal const float ControlsBottom=.047f,ControlsTop=.137f;
        public float BannerInsetPixels {get;private set;}
        internal static Rect SafeArea(float bannerInsetPixels)
        {
            Rect safe=Screen.safeArea;
            safe.yMin=Mathf.Max(safe.yMin,bannerInsetPixels);
            return safe;
        }
        public void SetBannerInset(float pixels)
        {
            pixels=Mathf.Clamp(pixels,0,Screen.height*.25f);
            if(Mathf.Abs(BannerInsetPixels-pixels)<.5f)return;
            BannerInsetPixels=pixels;currentGame?.Reframe();
        }
        bool hudContentDirty=true;
        Core.GameState shownState;
        Text shownHomeCoins;
        string shownMechanicHint;
        int shownLevel,shownCoins,shownBeltGroups,shownCapacityGroups,shownPacked,shownEpoch;
        readonly int[] shownStocks=new int[4];
        Core.RunStatus shownStatus;
        bool shownUndo,shownShuffle,shownMoving,shownPaused,shownTestRun,shownEntering;
        public string DialogTitle {get;private set;}
        public RectTransform Root=>root;
        public RectTransform HomeRoot=>home;
        public RectTransform DialogRoot=>modal;
        public GameController CurrentGame=>currentGame;
        public Text LevelLabel=>level;
        public Text CapacityLabel=>capacity;
        public Text CoinsLabel=>coins;
        public Text HomeCoinsLabel=>homeCoins;
        public Text InstructionLabel=>instruction;
        public List<Text> BoosterCounts=>boosterCounts;
        public List<Button> BoosterButtons=>boosters;
        public List<GameObject> PricePlates=>pricePlates;
        public List<GameObject> StockBadges=>stockBadges;
        public ReferenceImage UndoBackground=>undoBackground;
        public Text FirstLabel(Button button)=>button.GetComponentInChildren<Text>();
        public RectTransform[] SkinRects()=>root.GetComponentsInChildren<RectTransform>(true);
        public Text SkinText(RectTransform target)=>target.GetComponent<Text>();
        public string SkinPath(RectTransform target)
        {
            string path=target.name;for(var parent=target.parent;parent!=null&&parent!=root;parent=parent.parent)
                if(parent.name!="Artwork content")path=parent.name+"/"+path;
            return path;
        }
        public bool IsHome=>home!=null&&home.gameObject.activeSelf;
        public bool HasDialog=>overlay!=null&&overlay.gameObject.activeSelf;
        Text level, capacity, toast, instruction,coins;
        readonly List<Text> boosterCounts = new List<Text>();
        readonly List<Button> boosters = new List<Button>();
        readonly List<GameObject> pricePlates=new List<GameObject>(),stockBadges=new List<GameObject>();
        readonly List<InputField> inputs=new List<InputField>();
        ReferenceImage undoBackground;
        Font font; float toastUntil;
        Button dialogClose;
        public void Build(Camera camera)
        {
            var work=BuildSteps(camera);
            try {while(work.MoveNext()){} }finally {(work as IDisposable)?.Dispose();}
        }
        internal System.Collections.IEnumerator BuildSteps(Camera camera)
        {
            font = GameAssets.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas=gameObject.AddComponent<Canvas>(); Canvas.renderMode=RenderMode.ScreenSpaceCamera; Canvas.worldCamera=camera; Canvas.planeDistance=2;
            Canvas.sortingOrder=100;
            var scaler=gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920); scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>(); root=Panel(transform as RectTransform,"Safe area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            if (FindObjectOfType<EventSystem>()==null) new GameObject("Input events",typeof(EventSystem),typeof(StandaloneInputModule));
            gameplay=Panel(root,"Playing Monitor",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var top=Panel(gameplay,"Top",new Vector2(0,.900f),new Vector2(1,.985f),Vector2.zero,Vector2.zero);
            var levelFrame=Art(top,"Level frame","levelPanel",new Vector2(.66f,.06f),new Vector2(.975f,.98f));
            levelFrame.color=Color.clear;
            level=Label(levelFrame.rectTransform,"Level","第 1 关",47,Vector2.zero,Vector2.one);level.fontStyle=FontStyle.Bold;
            var capacityFrame=Art(top,"Capacity frame","capacityPanel",new Vector2(.352f,.10f),new Vector2(.648f,.91f));
            capacity=IconValue(capacityFrame.rectTransform,"Capacity","beltCapacity","0 / 7",47,new Vector2(.025f,.02f),new Vector2(.975f,.98f));
            capacity.fontStyle=FontStyle.Bold;
            capacity.color=Color.white;
            var capacityIcon=capacity.transform.parent.Find("Icon").GetComponent<LayoutElement>();
            capacityIcon.preferredWidth=76;capacityIcon.preferredHeight=84;
            Button(top,"Settings","",new Vector2(.025f,.06f),new Vector2(.15f,.98f),()=>OnPause?.Invoke(),"settings");
            // Keep the Lua-facing value without adding a second row to the play HUD.
            // The visible wallet and mechanic guide live in Settings.
            coins=Label(top,"Coins","500000",27,Vector2.zero,Vector2.zero);coins.gameObject.SetActive(false);
            instruction=Label(gameplay,"Instruction","",27,new Vector2(.07f,.852f),new Vector2(.93f,.892f));
            instruction.gameObject.SetActive(false);
            yield return null;
            var footer=Panel(gameplay,"Controls",new Vector2(0,ControlsBottom),new Vector2(1,ControlsTop),Vector2.zero,Vector2.zero);
            string[] names={"Undo","Shuffle","Belt +","Truck +"},icons={"undo","shuffle","belt","truck"};
            Action[] handlers={() => OnUndo?.Invoke(),()=>OnShuffle?.Invoke(),()=>OnBelt?.Invoke(),()=>OnTruck?.Invoke()};
            for(int i=0;i<4;i++)
            {
                float left=.03f+i*.245f;
                var slot=Panel(footer,names[i],new Vector2(left,0),new Vector2(left+.205f,1),Vector2.zero,Vector2.zero);
                var button=Button(slot,names[i]+" Button","",Vector2.zero,Vector2.one,handlers[i],"boosterBg");
                if(i==0){undoBackground=button.GetComponent<ReferenceImage>();var colors=button.colors;colors.disabledColor=Color.white;button.colors=colors;}
                var iconInset=Vector2.one*(icons[i]=="truck"?.19f:.025f);
                var icon=Image(button.transform as RectTransform,"Icon",GameAssets.Sprite(icons[i]),iconInset,Vector2.one-iconInset);icon.preserveAspect=true;
                var price=Art(slot,"Ad badge","priceBadge",new Vector2(.64f,.72f),new Vector2(1.04f,1.02f)).rectTransform;
                Label(price,"Ad","▶ AD",19,new Vector2(.03f,.05f),new Vector2(.97f,.95f));
                price.gameObject.SetActive(false);
                var stock=Art(slot,"Inventory badge","boosterCount",new Vector2(.70f,-.025f),new Vector2(1.035f,.31f),true).rectTransform;
                var number=Label(stock,"Inventory","2",31,new Vector2(.08f,.08f),new Vector2(.92f,.92f));number.color=Color.black;number.fontStyle=FontStyle.Bold;
                pricePlates.Add(price.gameObject);stockBadges.Add(stock.gameObject);boosterCounts.Add(number);boosters.Add(button);
                yield return null;
            }
            toastPlate=Art(root,"Message plaque","toastPanel",new Vector2(.09f,.155f),new Vector2(.91f,.205f)).rectTransform;
            toast=Label(toastPlate,"Toast","",27,new Vector2(.05f,.08f),new Vector2(.95f,.92f));toast.color=Color.black;
            toastPlate.gameObject.SetActive(false);
            home=Panel(root,"Home Monitor",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            home.gameObject.SetActive(false);
            if(Application.isEditor||Debug.isDebugBuild)Button(root,"GM","GM",new Vector2(.865f,.79f),new Vector2(.985f,.835f),()=>
            {
                if(CurrentGame==null)return;
                CurrentGame.SetPaused(true);HotUpdate.LuaGameRuntime.Call("ui.pages","gm",this);
            },"button");
            BuildTutorialFocus();
            overlay=Panel(root,"Modal overlay",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            overlayBackground=Panel(overlay,"Dim",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var shade=overlayBackground.gameObject.AddComponent<Image>();shade.sprite=ArtTheme.WhiteSprite;shade.color=new Color(.17f,.15f,.30f,.68f);
            overlay.gameObject.SetActive(false);
            root.offsetMin=Vector2.zero;root.offsetMax=Vector2.zero;
            skinDirty=true;
        }
        void Update()
        {
            if(toastPlate!=null&&toastPlate.gameObject.activeSelf)
            {
                if(Time.unscaledTime>toastUntil){toast.text="";toastPlate.gameObject.SetActive(false);}
                else PositionToast();
            }
            FitInputFields();
            if(root!=null && Screen.width>0 && Screen.height>0)
            {
                Rect safe=SafeArea(BannerInsetPixels);
                Vector2 min=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height),max=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
                if(root.anchorMin!=min||root.anchorMax!=max)ApplySafeArea(min,max);
                if(HasDialog)FitDialog();
            }
            if(skinDirty&&HotUpdate.LuaGameRuntime.Ready){skinDirty=false;HotUpdate.LuaGameRuntime.Call("ui.skin","apply",this);}
        }
        void ApplySafeArea(Vector2 min,Vector2 max)
        {
            root.anchorMin=min;root.anchorMax=max;
            if(overlayBackground==null)return;
            // The dialog stays in the safe area. Convert canvas edges into its
            // parent's anchor coordinates so the shade reaches the screen edges.
            Vector2 span=max-min;
            if(span.x<=0||span.y<=0)return;
            var inverseSpan=new Vector2(1/span.x,1/span.y);
            overlayBackground.anchorMin=Vector2.Scale(-min,inverseSpan);
            overlayBackground.anchorMax=Vector2.Scale(Vector2.one-min,inverseSpan);
        }
        public void Refresh(GameController game)
        {
            currentGame=game;
            if(game.Progress.language!=UiLocalization.Current)UiLocalization.Select(game.Progress.language,false);
            bool changed=RefreshHudValues(game);
            if(changed)HotUpdate.LuaGameRuntime.Call("ui.pages","refresh",this,game);
            UpdateTutorialPointer(changed);
        }
        bool RefreshHudValues(GameController game)
        {
            // Compare the values actually displayed, not every moving fragment.
            // Lua still owns the formatting; unchanged frames allocate no UI strings.
            var state=game.State;var progress=game.Progress;
            int groupSize=state.GroupSize;
            int beltGroups=(state.BeltCount+groupSize-1)/groupSize,capacityGroups=state.BeltCapacity/groupSize;
            bool undo=game.UndoDepth>0,moving=state.BeltCount>0||state.PendingTransfers>0;
            bool changed=hudContentDirty||shownState!=state||shownHomeCoins!=homeCoins||
                shownMechanicHint!=game.MechanicHint||shownLevel!=game.Level.id||shownCoins!=progress.coins||
                shownBeltGroups!=beltGroups||shownCapacityGroups!=capacityGroups||shownPacked!=state.PackedCount||
                shownEpoch!=state.Epoch||shownStatus!=state.Status||shownUndo!=undo||
                shownShuffle!=game.SelectingShuffle||shownMoving!=moving||shownPaused!=game.Paused||shownTestRun!=game.IsTestRun||shownEntering!=game.IsEnteringLevel||
                shownStocks[0]!=progress.undo||shownStocks[1]!=progress.shuffle||shownStocks[2]!=progress.belt||shownStocks[3]!=progress.truck;
            if(!changed)return false;
            hudContentDirty=false;shownState=state;shownHomeCoins=homeCoins;shownMechanicHint=game.MechanicHint;
            shownLevel=game.Level.id;shownCoins=progress.coins;shownBeltGroups=beltGroups;shownCapacityGroups=capacityGroups;
            shownPacked=state.PackedCount;shownEpoch=state.Epoch;shownStatus=state.Status;shownUndo=undo;
            shownShuffle=game.SelectingShuffle;shownMoving=moving;shownPaused=game.Paused;shownTestRun=game.IsTestRun;shownEntering=game.IsEnteringLevel;
            shownStocks[0]=progress.undo;shownStocks[1]=progress.shuffle;shownStocks[2]=progress.belt;shownStocks[3]=progress.truck;
            return true;
        }
        public void Toast(string message) { toast.text=HotUpdate.LuaUi.Text(message);toastUntil=Time.unscaledTime+3f;toastPlate.gameObject.SetActive(!string.IsNullOrEmpty(message));PositionToast();toastPlate.SetAsLastSibling(); }
        void PositionToast()
        {
            Vector2 min=new Vector2(.09f,HasDialog ? .905f : .155f),max=new Vector2(.91f,HasDialog ? .96f : .205f);
            if(toastPlate.anchorMin!=min)toastPlate.anchorMin=min;
            if(toastPlate.anchorMax!=max)toastPlate.anchorMax=max;
        }
        public void Close() { CancelBoosterPresentation(false);StopCelebration();overlay.gameObject.SetActive(false);ClearCandyBackdrop(); }
        public void ShowGameplay(){home.gameObject.SetActive(false);if(homeArtwork!=null)homeArtwork.gameObject.SetActive(false);gameplay.gameObject.SetActive(true);PositionGmButton(false);Close();}
        void PositionGmButton(bool onHome)
        {
            var gm=root.Find("GM") as RectTransform;if(gm==null)return;
            gm.anchorMin=new Vector2(.865f,onHome?.865f:.79f);
            gm.anchorMax=new Vector2(.985f,onHome?.91f:.835f);
        }
        public void Begin(string title,string banner="ribbon")
        {
            currentGame?.SetShuffleSelection(false);
            IsSimpleMenu=false;
            CancelBoosterPresentation(false);
            StopCelebration();
            DialogTitle=title;skinDirty=true;
            candyResult=banner=="candyWinRibbon"||banner=="candyFailRibbon";
            bool candySettings=title=="设置"||title=="暂停";
            if(candyResult||candySettings)CaptureCandyBackdrop();
            else ClearCandyBackdrop();
            overlay.gameObject.SetActive(true);
            if(modal!=null){modal.gameObject.SetActive(false);ReleaseCallbacks(modal.gameObject);Destroy(modal.gameObject);}
            var center=Vector2.one*.5f;
            modal=Panel(overlay,"Dialog",candyResult?Vector2.zero:center,candyResult?Vector2.one:center,Vector2.zero,Vector2.zero);
            if(!candyResult)
            {
                modal.sizeDelta=new Vector2(900,1120);FitDialog();
                var bg=modal.gameObject.AddComponent<ReferenceImage>();bg.SetArtwork("frame");
            }
            // Direct child of the frame so other dialogs can find and position its title.
            var chrome=new GameObject("Dialog chrome",typeof(RectTransform)).GetComponent<RectTransform>();
            chrome.SetParent(modal,false);
            chrome.anchorMin=Vector2.zero;chrome.anchorMax=Vector2.one;
            chrome.offsetMin=chrome.offsetMax=Vector2.zero;
            if(candyResult)
            {
                Art(chrome,"Result ribbon",banner,new Vector2(.085f,.724f),new Vector2(.915f,.800f));
                Label(chrome,"Title",title,66,new Vector2(.18f,.739f),new Vector2(.82f,.789f));
                dialogClose=null;
                return;
            }
            if(candySettings)BuildCandyHeader(chrome);
            Label(chrome,"Title",title,candySettings?58:48,
                new Vector2(.13f,candySettings?.883f:.89f),new Vector2(.87f,candySettings?.963f:.98f));
            dialogClose=Button(chrome,"Close","",new Vector2(.874f,.912f),new Vector2(1.004f,1.014f),()=>OnCancel?.Invoke(),"close");
        }
        void FitDialog()
        {
            if(candyResult||modal==null||root.rect.width<=0||root.rect.height<=0)return;
            if(DialogTitle=="七日签到")
            {
                // Use the portrait viewport fully, while keeping enough content
                // height for all seven cards on wide/landscape screens.
                float width=root.rect.width*.96f,height=root.rect.height*.86f;
                float signinScale=Mathf.Min(width/1000f,height/1460f);
                var size=new Vector2(1000f,height/signinScale);
                if(modal.sizeDelta!=size)modal.sizeDelta=size;
                if(!Mathf.Approximately(modal.localScale.x,signinScale))modal.localScale=Vector3.one*signinScale;
                modal.anchoredPosition=new Vector2(0,-root.rect.height*.012f);
                return;
            }
            bool levelSelection=DialogTitle=="选择关卡";
            bool settings=DialogTitle=="设置"||DialogTitle=="暂停";
            float widthLimit=settings?.94f:levelSelection?.94f:.88f,heightLimit=levelSelection?.84f:.78f;
            float scale=Mathf.Min(1,Mathf.Min(root.rect.width*widthLimit/Mathf.Max(1,modal.sizeDelta.x),root.rect.height*heightLimit/Mathf.Max(1,modal.sizeDelta.y)));
            if(!Mathf.Approximately(modal.localScale.x,scale))modal.localScale=Vector3.one*scale;
            modal.anchoredPosition=new Vector2(0,root.rect.height*(DialogTitle=="设置"?-.005f:DialogTitle=="选择关卡"?-.012f:0));
        }
        public void Pause(PlayerProgress progress)=>HotUpdate.LuaUi.Render("pause",this,progress);
        public void Result(bool won)=>HotUpdate.LuaUi.Render("result",this,won);
        public void Levels(Core.LevelCatalog catalog,int current,int page=0)=>HotUpdate.LuaUi.Render("levels",this,catalog,current,page);
        public void Notice(string message)=>HotUpdate.LuaUi.Render("notice",this,message);
        static RectTransform Panel(RectTransform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var artwork=parent.GetComponent<ReferenceImage>();if(artwork!=null)parent=artwork.ContentRoot;
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
            rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;return rt;
        }
        public Image Image(RectTransform parent,string name,Sprite sprite,Vector2 min,Vector2 max)
        {
            var rt=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite??ArtTheme.WhiteSprite;image.raycastTarget=false;return image;
        }
        public ReferenceImage Art(RectTransform parent,string name,string artwork,Vector2 min,Vector2 max,bool aspect=false)
        {
            var rt=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);var image=rt.gameObject.AddComponent<ReferenceImage>();
            image.SetArtwork(artwork);image.raycastTarget=false;image.preserveAspect=aspect;image.ConfigureGeometry();
            return image;
        }
        public Text Label(RectTransform parent,string name,string value,int size,Vector2 min,Vector2 max)
        {
            size=Mathf.RoundToInt(size*1.25f);
            var rt=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);var text=rt.gameObject.AddComponent<LocalizedText>();
            text.font=font;text.text=HotUpdate.LuaUi.Text(value);text.fontStyle=FontStyle.Normal;text.fontSize=size;text.color=Color.black;text.alignment=TextAnchor.MiddleCenter;
            text.alignByGeometry=true;text.raycastTarget=false;text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=8;text.resizeTextMaxSize=size;
            return text;
        }
        public Text Paragraph(RectTransform parent,string name,string value,int size,Vector2 min,Vector2 max)
        {
            var text=Label(parent,name,value,size,min,max);text.horizontalOverflow=HorizontalWrapMode.Wrap;text.lineSpacing=1.08f;
            if(text is LocalizedText localized){localized.WrapCjkCharacters=true;localized.RefreshLanguage();}
            return text;
        }
        public Text IconValue(RectTransform parent,string name,string artwork,string value,int size,Vector2 min,Vector2 max)
        {
            var row=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;
            layout.padding=new RectOffset(8,8,4,4);layout.spacing=10;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
            var icon=Art(row,"Icon",artwork,Vector2.zero,Vector2.one,true);
            var iconSize=icon.gameObject.AddComponent<LayoutElement>();iconSize.minWidth=size*.9f;iconSize.minHeight=0;iconSize.preferredWidth=size*1.2f;iconSize.preferredHeight=size*1.2f;
            var text=Label(row,"Value",value,size,Vector2.zero,Vector2.one);
            var textSize=text.gameObject.AddComponent<LayoutElement>();textSize.minWidth=0;textSize.minHeight=0;textSize.preferredHeight=text.fontSize*1.5f;
            return text;
        }
        public Button Button(RectTransform parent,string name,string text,Vector2 min,Vector2 max,Action click,string sprite)
        {
            var rt=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);var image=rt.gameObject.AddComponent<ReferenceImage>();
            image.SetArtwork(sprite);
            image.preserveAspect=sprite=="close"||sprite=="settings"||sprite=="boosterBg"||sprite=="boosterGrey"||sprite=="toggleOn"||sprite=="toggleOff";
            image.ConfigureGeometry();
            image.color=image.sprite==null?ArtTheme.Plum:Color.white;
            var b=rt.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>click?.Invoke());
            var colors=b.colors;colors.pressedColor=new Color(.82f,.77f,.94f);colors.disabledColor=new Color(.81f,.78f,.90f,.82f);b.colors=colors;
            if(!string.IsNullOrEmpty(text))
            {
                var label=Label(rt,"Text",text,39,new Vector2(.05f,.04f),new Vector2(.95f,.96f));
                label.rectTransform.offsetMin=new Vector2(8,3);label.rectTransform.offsetMax=new Vector2(-8,-3);
            }
            return b;
        }
    }
}

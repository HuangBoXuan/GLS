using System;
using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        public bool IsSimpleMenu {get;private set;}
        public void BeginSimpleMenu(string title,float height)
        {
            Begin(title);IsSimpleMenu=true;modal.sizeDelta=new Vector2(960,height);
            modal.GetComponent<ReferenceImage>().SetArtwork("paper");
            var chrome=modal.Find("Dialog chrome");chrome.Find("Title").gameObject.SetActive(false);
            var close=dialogClose.GetComponent<RectTransform>();close.anchorMin=new Vector2(.88f,.91f);close.anchorMax=new Vector2(.985f,.99f);
            dialogClose.GetComponent<ReferenceImage>().SetArtwork("close");
            foreach(Transform child in close)child.gameObject.SetActive(false);
            var heading=Label(modal,"Menu title",title,43,new Vector2(.16f,.905f),new Vector2(.84f,.983f));heading.fontStyle=FontStyle.Bold;
            var rule=Panel(modal,"Dotted divider",new Vector2(.10f,.891f),new Vector2(.90f,.894f),Vector2.zero,Vector2.zero);
            for(int i=0;i<42;i++)
            {
                var dash=Panel(rule,"Dash",new Vector2(i/42f,0),new Vector2((i+.45f)/42f,1),Vector2.zero,Vector2.zero);
                dash.gameObject.AddComponent<Image>().color=new Color(.83f,.60f,.35f,.55f);
            }
            FitDialog();
        }
        public Button TextButton(RectTransform parent,string name,string text,Vector2 min,Vector2 max,Action action)
        {
            var rect=Panel(parent,name,min,max,Vector2.zero,Vector2.zero);var image=rect.gameObject.AddComponent<Image>();image.color=Color.clear;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action?.Invoke());
            Label(rect,"Text",text,28,Vector2.zero,Vector2.one);return button;
        }
    }
    public sealed partial class GameController
    {
        public string SelectedLanguageName=>UiLocalization.LanguageName(Progress.language);
        public string ClientVersion=>Application.version;
        public void SelectLanguage(string code)
        {
            if(code!="en"&&code!="zh-CN"&&code!="hi"&&code!="pt-BR")return;
            Progress.language=code;SaveProgress();UiLocalization.Select(code,!IsTestRun);
            Hud.MarkSkinDirty();Hud.Pause(Progress);
        }
    }
}

using UnityEngine.UI;
using System.Text;

namespace LoopSort0
{
    public sealed class LocalizedText:Text
    {
        string source="";
        public bool TranslateSource=true;
        public bool WrapCjkCharacters;
        public string SourceText=>source;
        public string DisplayText=>base.text;
        public override string text
        {
            get=>base.text;
            set {if(value==base.text)return;source=value??"";RefreshLanguage();}
        }
        protected override void OnEnable(){base.OnEnable();UiLocalization.Changed+=RefreshLanguage;RefreshLanguage();}
        protected override void OnDisable(){UiLocalization.Changed-=RefreshLanguage;base.OnDisable();}
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if(WrapCjkCharacters)RefreshLanguage();
        }
        string WrapCjk(string value)
        {
            bool cjk=false;foreach(char c in value)if(c>='\u2e80'&&c<='\u9fff'){cjk=true;break;}
            float width=rectTransform.rect.width-4;
            if(!cjk||font==null||width<=0)return value;
            int size=resizeTextForBestFit?resizeTextMaxSize:fontSize;
            if(font.dynamic)font.RequestCharactersInTexture(value,size,fontStyle);
            var result=new StringBuilder();float used=0;
            foreach(char c in value)
            {
                if(c=='\n'){result.Append(c);used=0;continue;}
                float advance=font.GetCharacterInfo(c,out var glyph,size,fontStyle)?glyph.advance:size;
                // Keep closing punctuation attached to its preceding character.
                bool closing=c=='，'||c=='。'||c=='！'||c=='？'||c=='、'||c=='；'||c=='：'||c=='）'||c=='》';
                if(used>0&&used+advance>width&&!closing){result.Append('\n');used=0;}
                result.Append(c);used+=advance;
            }
            return result.ToString();
        }
        public void RefreshLanguage()
        {
            string translated=TranslateSource?UiLocalization.Translate(source):source;
            var selected=UiLocalization.FontFor(translated);if(selected!=null&&font!=selected)font=selected;
            // Legacy best fit treats uninterrupted CJK as one word. Wrap only
            // paragraphs, using actual font advances when text or layout changes.
            if(WrapCjkCharacters)translated=WrapCjk(translated);
            base.text=UiLocalization.Shape(translated);
        }
    }
}

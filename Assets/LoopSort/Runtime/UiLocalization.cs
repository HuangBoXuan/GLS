using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    public static class UiLocalization
    {
        const string Preference="LoopSort.Ui.Language";
        [Serializable] public sealed class Entry {public string source,en,hi,pt;}
        [Serializable] sealed class Catalog {public Entry[] entries;}
        [Serializable] sealed class HindiWord {public string word,glyph;}
        [Serializable] sealed class HindiCatalog {public HindiWord[] words;}
        sealed class Pattern {public Regex regex;public Entry entry;}
        static string current;
        static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.Ordinal);
        static readonly Dictionary<string,string> hindiWords=new Dictionary<string,string>(StringComparer.Ordinal);
        static readonly List<Pattern> patterns=new List<Pattern>();
        static readonly List<Entry> tokens=new List<Entry>();
        static bool loaded;
        static Font hindiFont,roundedFont;
        public static event Action Changed;
        public static string Current=>current??(current=Normalize(PlayerPrefs.GetString(Preference,"zh-CN")));
        public static string Normalize(string code)=>code=="en"||code=="hi"||code=="pt-BR"?code:"zh-CN";
        public static string LanguageName(string code)=>code=="en"?"English":code=="hi"?"हिन्दी":code=="pt-BR"?"Português":"简体中文";
        public static void Select(string code,bool persist)
        {
            code=Normalize(code);
            if(persist){PlayerPrefs.SetString(Preference,code);PlayerPrefs.Save();}
            if(Current==code)return;
            current=code;cache.Clear();Changed?.Invoke();
        }
        static void Load()
        {
            if(loaded)return;loaded=true;
            var asset=Resources.Load<TextAsset>("LoopSort/Localization/catalog");
            if(asset!=null)
                foreach(var entry in JsonConvert.DeserializeObject<Catalog>(asset.text).entries)
                {
                    entries[entry.source]=entry;
                    if(entry.source.Contains("{0}"))
                    {
                        string regex=Regex.Escape(entry.source);
                        regex=Regex.Replace(regex,@"\\\{\d+}","(.+?)");
                        patterns.Add(new Pattern{entry=entry,regex=new Regex("^"+regex+"$",RegexOptions.CultureInvariant)});
                    }
                    else if(entry.source.Length>0)tokens.Add(entry);
                }
            tokens.Sort((a,b)=>b.source.Length.CompareTo(a.source.Length));
            var hindi=Resources.Load<TextAsset>("LoopSort/Localization/hindi-shaping");
            if(hindi!=null)foreach(var word in JsonConvert.DeserializeObject<HindiCatalog>(hindi.text).words)hindiWords[word.word]=word.glyph;
        }
        static string Value(Entry entry)=>Current=="en"?entry.en:Current=="hi"?entry.hi:Current=="pt-BR"?entry.pt:entry.source;
        public static string Translate(string source)
        {
            if(string.IsNullOrEmpty(source)||Current=="zh-CN")return source??"";
            Load();if(cache.TryGetValue(source,out string value))return value;
            value=TranslateCore(source,0);cache[source]=value;return value;
        }
        static string TranslateCore(string source,int depth)
        {
            if(entries.TryGetValue(source,out var direct))return Value(direct);
            if(depth<3)
                foreach(var pattern in patterns)
                {
                    var match=pattern.regex.Match(source);if(!match.Success)continue;
                    var arguments=new object[match.Groups.Count-1];
                    for(int i=0;i<arguments.Length;i++)arguments[i]=TranslateCore(match.Groups[i+1].Value,depth+1);
                    return string.Format(CultureInfo.InvariantCulture,Value(pattern.entry),arguments);
                }
            // Compound reward/status labels are assembled from named game terms.
            // Replace only known source phrases, never arbitrary player input.
            var result=new StringBuilder();
            for(int offset=0;offset<source.Length;)
            {
                Entry found=null;
                foreach(var entry in tokens)
                    if(offset+entry.source.Length<=source.Length&&string.CompareOrdinal(source,offset,entry.source,0,entry.source.Length)==0){found=entry;break;}
                if(found==null){result.Append(source[offset++]);continue;}
                result.Append(Value(found));offset+=found.source.Length;
            }
            return result.ToString();
        }
        public static bool IsHindi(char c)=>(c>='\u0900'&&c<='\u097f')||c=='\u200c'||c=='\u200d';
        public static bool ContainsHindi(string text)
        {if(text!=null)foreach(char c in text)if(IsHindi(c))return true;return false;}
        public static string Shape(string text)
        {
            if(!ContainsHindi(text))return text;
            Load();var result=new StringBuilder();
            for(int i=0;i<text.Length;)
            {
                if(!IsHindi(text[i])){result.Append(text[i++]);continue;}
                int start=i++;while(i<text.Length&&IsHindi(text[i]))i++;
                string word=text.Substring(start,i-start);result.Append(hindiWords.TryGetValue(word,out string glyph)?glyph:word);
            }
            return result.ToString();
        }
        public static Font FontFor(string text)
        {
            if(ContainsHindi(text))return hindiFont??(hindiFont=Resources.Load<Font>("LoopSort/Fonts/LoopSortHindi"));
            // A whole label keeps one font. Mixed CJK labels retain their complete
            // glyph coverage; Latin/Portuguese and counters use the rounded face.
            var rounded=roundedFont??(roundedFont=Resources.Load<Font>("LoopSort/Fonts/LoopSortRounded-ExtraBold"));
            if(rounded!=null&&!string.IsNullOrEmpty(text))
            {
                bool supported=true;
                // Unity's HasCharacter can succeed through an OS fallback. Restrict
                // the script explicitly so Chinese never uses a thin system font.
                foreach(char c in text)
                    if(!char.IsWhiteSpace(c)&&((c>'\u024f'&&(c<'\u2000'||c>'\u206f'))||!rounded.HasCharacter(c)))
                    {supported=false;break;}
                if(supported)return rounded;
            }
            return GameAssets.Font;
        }
    }

}

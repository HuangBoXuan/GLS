using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopSort0.HotUpdate
{
    public static class LuaUi
    {
        static readonly Dictionary<string,Color> colors=new Dictionary<string,Color>();
        public static Color Color(string name,Color fallback)
        {
            if(!LuaGameRuntime.Ready)return fallback;
            if(!colors.TryGetValue(name,out var color))colors[name]=color=(Color)LuaGameRuntime.Call("ui.skin","color",name,fallback);
            return color;
        }
        public static string Text(string value)=>LuaGameRuntime.Ready?(string)LuaGameRuntime.Call("ui.skin","text",value):value;
        public static bool Render(string page,GameHud hud,params object[] arguments)
        {
            if(!LuaGameRuntime.Ready)return false;
            var args=new object[arguments.Length+2];args[0]=page;args[1]=hud;Array.Copy(arguments,0,args,2,arguments.Length);
            return Convert.ToBoolean(LuaGameRuntime.Call("ui.pages","render",args));
        }
        public static void Clear(){colors.Clear();}
    }
}

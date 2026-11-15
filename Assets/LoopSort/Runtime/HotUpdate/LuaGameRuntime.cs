using System;
using System.Collections.Generic;
using LoopSort0.Core;
using XLua;

namespace LoopSort0.HotUpdate
{
    public static class LuaGameRuntime
    {
        static LuaEnv environment;
        static readonly Dictionary<string,LuaTable> modules=new Dictionary<string,LuaTable>();
        static readonly Dictionary<string,LuaFunction> functions=new Dictionary<string,LuaFunction>();
        public static bool Ready {get;private set;}
        public static void Initialize()
        {
            var work=InitializeIncrementally();
            try {while(work.MoveNext()){} }finally {(work as IDisposable)?.Dispose();}
        }
        internal static System.Collections.IEnumerator InitializeIncrementally(Action<float> progress=null)
        {
            if(Ready)yield break;
            if(environment!=null)Shutdown();
            environment=new LuaEnv();environment.AddLoader(LoadModule);
            bool success=false;
            try
            {
                // A native DLL stays loaded until the editor process exits, even
                // after its old Assets path has been removed during migration.
                if(environment.Global.Get<string>("_VERSION")!="Lua 5.4")
                    throw new InvalidOperationException("当前仍加载旧版 Lua 运行库，请完全退出并重新打开 Unity；Android 请安装重新构建的 SDK 版 APK。");
                yield return null;
                string[] names={"game.rules","game.flow","ui.skin","ui.assets","ui.pages"};
                for(int i=0;i<names.Length;i++){Module(names[i]);progress?.Invoke((i+1f)/names.Length);yield return null;}
                Call("game.config","configure_engine");
                GameState.EngineFactory=data=>new LuaRules(data);
                FragmentLayout.Resolver=level=>(FragmentLayout)Call("game.rules","fragment_layout",level);
                Ready=true;success=true;
            }
            finally {if(!success)Shutdown();}
        }
        static bool LoadModule(ref string name,ModuleLuaBuffer buffer)
        {
            byte[] bytes=HotContent.Lua(name);
            if(bytes==null)return false;
            buffer.Copy(bytes);name="lua/"+name.Replace('.','/')+".lua";return true;
        }
        static LuaTable Module(string name)
        {
            if(modules.TryGetValue(name,out var module))return module;
            using(var require=environment.Global.Get<LuaFunction>("require"))
                module=InvokeFirst(require,name) as LuaTable
                    ??throw new InvalidOperationException("Lua module must return a table: "+name);
            modules[name]=module;return module;
        }
        public static object Call(string module,string name,params object[] arguments)
        {
            if(environment==null)throw new InvalidOperationException("Lua is not initialized.");
            string key=module+":"+name;
            if(!functions.TryGetValue(key,out var function))functions[key]=function=Module(module).Get<LuaFunction>(name)
                ??throw new MissingMethodException("Lua function missing: "+key);
            return InvokeFirst(function,arguments);
        }
        static object InvokeFirst(LuaFunction function,params object[] arguments)
        {
            // xLua returns null, rather than an empty array, for Lua functions
            // with zero return values (e.g. configure_engine and initialize).
            object[] results=function.Call(arguments);
            return results!=null&&results.Length>0?results[0]:null;
        }
        public static void Tick(){environment?.Tick();}
        public static void Shutdown()
        {
            Ready=false;
            LuaUi.Clear();
            RuntimeTuning.Clear();
            GameState.EngineFactory=null;FragmentLayout.Resolver=null;
            foreach(var function in functions.Values)function.Dispose();functions.Clear();
            foreach(var module in modules.Values)module.Dispose();modules.Clear();
            if(environment==null)return;
            environment.FullGc();GC.Collect();GC.WaitForPendingFinalizers();environment.Tick();environment.Dispose();environment=null;
        }
        sealed class LuaRules:IGameRuleEngine
        {
            LuaTable state;
            readonly Dictionary<string,LuaFunction> methods=new Dictionary<string,LuaFunction>();
            public LuaRules(RuleStateData data){state=(LuaTable)Call("game.rules","new",data);}
            LuaFunction Method(string name)
            {
                if(!methods.TryGetValue(name,out var function))methods[name]=function=state.Get<LuaFunction>(name)
                    ??throw new MissingMethodException("Lua rule missing: "+name);
                return function;
            }
            // Generic xLua calls avoid boxing argument/result arrays for each
            // fragment. Keep the less frequent DTO calls on the general bridge.
            public int InvokeInt(string name,int value)=>Method(name).Func<LuaTable,int,int>(state,value);
            public int InvokeInt(string name,int first,int second)=>Method(name).Func<LuaTable,int,int,int>(state,first,second);
            public bool InvokeBool(string name,int first,int second)=>Method(name).Func<LuaTable,int,int,bool>(state,first,second);
            public void Tick(float delta,bool hold)=>Method("tick").Func<LuaTable,float,bool,object>(state,delta,hold);
            public object Invoke(string name,params object[] arguments)
            {
                var function=Method(name);
                var values=new object[arguments.Length+1];values[0]=state;Array.Copy(arguments,0,values,1,arguments.Length);
                return InvokeFirst(function,values);
            }
            public void Dispose(){foreach(var f in methods.Values)f.Dispose();methods.Clear();state?.Dispose();state=null;}
        }
    }
}

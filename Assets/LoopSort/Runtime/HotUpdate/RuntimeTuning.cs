using System.Collections.Generic;

namespace LoopSort0.HotUpdate
{
    // Cached engine inputs: physics does not cross the Lua boundary per fragment.
    public static class RuntimeTuning
    {
        static readonly Dictionary<string,float> values=new Dictionary<string,float>();
        public static void Set(string name,float value){values[name]=value;}
        public static float Get(string name,float fallback)=>values.TryGetValue(name,out float value)?value:fallback;
        public static void Clear()=>values.Clear();
    }
}

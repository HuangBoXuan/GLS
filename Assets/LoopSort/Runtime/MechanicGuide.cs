using System;
using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;

namespace LoopSort0
{
    [Serializable] public sealed class MechanicGuide
    {
        public int id;public string title,sprite,description;public float scale;
        [Serializable] sealed class Catalog {public MechanicGuide[] guides;}
        static MechanicGuide[] all;
        public static MechanicGuide[] All=>all??(all=GameAssets.Data<Catalog>("mechanic-guides").guides);
        public static IEnumerable<MechanicGuide> ForLevel(LevelData level)
        {
            var ids=new HashSet<int>((int[])HotUpdate.LuaGameRuntime.Call("game.flow","guide_ids",level));
            return All.Where(g=>ids.Contains(g.id));
        }
    }
}

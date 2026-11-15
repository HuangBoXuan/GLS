using System;

namespace LoopSort0.Core
{
    public sealed class FragmentLayout
    {
        public static Func<LevelData,FragmentLayout> Resolver;
        public readonly int Columns,Rows,Depth;
        public int Count=>Columns*Rows*Depth;
        public float CellSize=>(float)Math.Min(1.503/Columns,Math.Min(1.503/Rows,1.002/Depth))*.97f;
        public FragmentLayout(int columns,int rows,int depth){Columns=columns;Rows=rows;Depth=depth;}
        public static FragmentLayout ForLevel(LevelData level)
        {
            if(Resolver==null)throw new InvalidOperationException("Lua fragment layout resolver is not initialized.");
            return Resolver(level);
        }
    }
}

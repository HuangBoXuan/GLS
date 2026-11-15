using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameHud
    {
        public void AdBreakArtwork(RectTransform parent,Vector2 min,Vector2 max)
        {
            Art(parent,"Pause banner icon","advertPaused",min,max,true);
        }
    }
}

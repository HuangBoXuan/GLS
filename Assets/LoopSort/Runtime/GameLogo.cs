using UnityEngine;
using UnityEngine.UI;

namespace LoopSort0
{
    internal static class GameLogo
    {
        // Shared packaged artwork also works before Lua/hot content initializes.
        internal static void Create(Transform parent)
        {
            var texture=Resources.Load<Texture2D>("LoopSort/Theme/candyloop-logo");
            if(texture==null)return;
            var holder=(RectTransform)new GameObject("CandyLoop Sort logo area",typeof(RectTransform)).transform;
            holder.SetParent(parent,false);
            holder.anchorMin=new Vector2(.04f,.67f);holder.anchorMax=new Vector2(.96f,.85f);
            holder.offsetMin=holder.offsetMax=Vector2.zero;
            var image=new GameObject("CandyLoop Sort logo",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).GetComponent<RawImage>();
            image.transform.SetParent(holder,false);image.texture=texture;image.raycastTarget=false;
            var aspect=image.GetComponent<AspectRatioFitter>();
            aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=(float)texture.width/texture.height;
        }
    }
}

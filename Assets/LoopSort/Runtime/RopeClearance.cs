using UnityEngine;

namespace LoopSort0
{
    internal static class RopeClearance
    {
        public static Bounds Bounds(Transform carrier)
        {
            var art=carrier.Find(VolcanoArt.ArtName);
            return art!=null&&art.TryGetComponent<Renderer>(out var renderer)?renderer.bounds:carrier.GetComponent<BoxCollider>().bounds;
        }
    }
}

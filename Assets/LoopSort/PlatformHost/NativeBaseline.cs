using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace LoopSort.PlatformLocalHost
{
    // Closed array types used through System.Array.CreateInstance in downloaded
    // Lua. Explicit AOT roots supplement the SDK's Unity/native bridge baseline.
    [Preserve]
    public static class NativeBaseline
    {
        [Preserve]
        public static Array[] ArrayTypes() => new Array[] {
            new Matrix4x4[0], new Vector4[0], new Vector3[0], new Vector2[0],
            new Color[0], new int[0], new float[0], new Mesh[0], new Material[0], new Type[0]
        };
    }
}

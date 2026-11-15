using System;
using System.Collections.Generic;
using LoopSort0.Core;
using LoopSort0.HotUpdate;
using UnityEngine;

namespace LoopSort0.Editor
{
    public static class LuaBindingConfig
    {
        // This inventory describes the existing standalone game's reflection
        // bridge. It does not add these game-specific types to a platform host.
        public const string SdkVersion="0.2.0-localtest.9730783e9569c94e";
        public const string LuaVersion="5.4.8";
        public const string BindingMode="standalone-reflection-host-api7-v1";
        public const string LinkerPath="Assets/LoopSort/Runtime/HotUpdate/link.xml";
        public const string LinkerContents="<linker>\n"
            +"  <!-- Transitional standalone shell only. These assemblies are not downloadable platform capabilities. -->\n"
            +"  <assembly fullname=\"LoopSort0.Core\" preserve=\"all\" />\n"
            +"  <assembly fullname=\"LoopSort0.Runtime\" preserve=\"all\" />\n"
            +"  <assembly fullname=\"mscorlib\">\n"
            +"    <type fullname=\"System.Random\" preserve=\"all\" />\n"
            +"    <type fullname=\"System.Collections.Generic.List`1\" preserve=\"all\" />\n"
            +"    <type fullname=\"System.Collections.Generic.Dictionary`2\" preserve=\"all\" />\n"
            +"  </assembly>\n"
            +"</linker>\n";
        public static List<Type> Types=>new List<Type> {
            typeof(RuleStateData),typeof(RuleArrays),typeof(FragmentLayout),typeof(GameState),typeof(GameState.KeyCollection),
            typeof(PieceState),typeof(CarrierState),typeof(ReleaseResult),typeof(StateSnapshot),typeof(LevelCatalog),typeof(LevelData),
            typeof(CarrierData),typeof(CargoGroup),typeof(TrackPoint),typeof(PieceLocation),typeof(RunStatus),typeof(FailReason),typeof(ScreenOffset),
            typeof(PlayerProgress),typeof(GameController),typeof(GameHud),typeof(LocalizedText),typeof(MechanicGuide),
            typeof(List<int>),typeof(List<string>),typeof(List<PieceState>),typeof(List<CarrierState>),typeof(List<GameState.KeyCollection>),
            typeof(System.Random),typeof(Vector2),typeof(Color),typeof(RectTransform),typeof(UnityEngine.UI.Text),
            typeof(UnityEngine.UI.Button),typeof(UnityEngine.UI.InputField),typeof(UnityEngine.GameObject)
            ,typeof(ReferenceImage),typeof(List<UnityEngine.UI.Text>),typeof(List<UnityEngine.UI.Button>),typeof(List<GameObject>),
            typeof(ArtTheme),typeof(ThemeSpriteDefinition),typeof(Material),typeof(RuntimeTuning),typeof(Dictionary<string,string>)
        };
        // Action callbacks use the host's static NativeDelegateBridge. The Func
        // is created in C# and invoked from Lua, never converted from Lua.
        public static List<Type> Delegates=>new List<Type>{typeof(Action),typeof(Action<int>),typeof(Action<string>),typeof(Func<string,bool>)};
    }
}

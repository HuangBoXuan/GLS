using System;
using System.Collections.Generic;
using System.Linq;

namespace LoopSort0.Core
{
    [Serializable] public sealed class LevelCatalog { public string version; public LevelData[] levels; }
    [Serializable] public sealed class TrackPoint { public int order; public float x, y, z; }
    [Serializable] public sealed class CargoGroup
    {
        public int color, keyColor; public bool hidden, sticky; public string raw;
    }
    [Serializable] public sealed class CarrierData
    {
        public string id; public float x, y=-1, z, yaw; public CargoGroup[] groups; public int curtainColor;
        public bool barrier, generator; public int tagColor;
        public int capacityGroups=4, targetColor;
        public string[] iceRequires, ropeLocks; public int[] lockColors;
        public string[] carpetLocks; public int carpetOrder;
    }
    [Serializable] public sealed class LevelData
    {
        public int id, slots, colorMix, sourceCarrier, sourceSpline, subdivide;
        public string theme; public float cameraRotation; public bool closed;
        // Calculated from the same sampled spline that drives the conveyor.
        public float splineLength, timeLimitSeconds;
        public ScreenOffset cameraOffset;
        public TrackPoint[] points; public CarrierData[] carriers, spares; public string[] unsupported;
        public bool IsSupported => unsupported == null || unsupported.Length == 0;
        public int PieceCount {get{int count=FragmentLayout.ForLevel(this).Count;return carriers.Sum(c=>(c.groups?.Length??0)*count);}}
    }
    [Serializable] public sealed class ScreenOffset {public float X,Y;}
    public enum PieceLocation { Cargo, Releasing, Belt, Receiving, Packed }
    public enum RunStatus { Playing, Won, Failed }
    public enum FailReason { None=-1, ConveyorFull=0, CantCompleteCarrier=1, TimeUp=2 }
    [Serializable] public sealed class PieceState
    {
        public int id, color, carrier; public bool hidden;
        public int keyColor; public bool sticky; public int bondedGroup=-1;
        public int transferVersion;
        public int receiveTarget=-1;
        public int loopCarrier=-1, loopCount;
        public PieceLocation location;
        public PieceState Copy() => (PieceState)MemberwiseClone();
    }
    [Serializable] public sealed class CarrierState
    {
        public int index; public string id; public bool packed;
        public List<int> cargo = new List<int>();
        public List<int> incoming = new List<int>();
        public int releasing;
        public int lastReleasedColor;
        public int curtainColor; public bool curtainOpen=true;
        public bool barrier, generator, iceBroken; public int tagColor;
        public int capacityGroups=4, targetColor;
        public string[] iceRequires=Array.Empty<string>();
        public List<int> lockColors=new List<int>(); public List<string> ropeLocks=new List<string>();
        public List<string> carpetLocks=new List<string>(); public int carpetOrder;
        public bool Frozen=>iceRequires.Length>0&&!iceBroken;
        public bool Accessible=>curtainOpen&&!Frozen&&lockColors.Count==0&&carpetLocks.Count==0;
        public bool CanEmit=>Accessible&&!barrier&&ropeLocks.Count==0;
        public CarrierState Copy() => new CarrierState { index = index, id = id, packed = packed,
            cargo = new List<int>(cargo), incoming = new List<int>(incoming), releasing = releasing,lastReleasedColor=lastReleasedColor,curtainColor=curtainColor,curtainOpen=curtainOpen,
            barrier=barrier,generator=generator,iceBroken=iceBroken,tagColor=tagColor,capacityGroups=capacityGroups,targetColor=targetColor,iceRequires=(string[])iceRequires.Clone(),
            lockColors=new List<int>(lockColors),ropeLocks=new List<string>(ropeLocks),
            carpetLocks=new List<string>(carpetLocks),carpetOrder=carpetOrder };
    }
    public sealed class StateSnapshot
    {
        public PieceState[] pieces; public CarrierState[] carriers;
        public int beltCapacity; public RunStatus status;
        public FailReason failReason=FailReason.None;
        public float elapsedTime, cannotCompleteTime;
    }
    public sealed class ReleaseResult
    {
        public bool accepted; public string reason; public int[] pieces = Array.Empty<int>();
        public int[] interruptedReceives = Array.Empty<int>();
    }
}

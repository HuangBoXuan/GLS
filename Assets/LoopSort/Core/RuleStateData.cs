using System;
using System.Collections.Generic;

namespace LoopSort0.Core
{
    // Typed view data, not a second rule implementation. Lua owns every mutation.
    public sealed class RuleStateData
    {
        public FragmentLayout fragments;
        public List<PieceState> pieces=new List<PieceState>();
        public List<CarrierState> carriers=new List<CarrierState>();
        public List<GameState.KeyCollection> keyCollections=new List<GameState.KeyCollection>();
        public int beltCapacity,beltCount,beltOccupiedSlots,pendingTransfers,packedCount,epoch,featureRevision;
        public RunStatus status=RunStatus.Playing;
        public FailReason failReason=FailReason.None;
        public float elapsedTime,cannotCompleteTime,conveyorSpeedScale=1;
    }
    public interface IGameRuleEngine:IDisposable
    {
        object Invoke(string method,params object[] arguments);
        int InvokeInt(string method,int value);
        int InvokeInt(string method,int first,int second);
        bool InvokeBool(string method,int first,int second);
        void Tick(float delta,bool hold);
    }
    public static class RuleArrays
    {
        public static int[] Ints(int count)=>new int[count];
        public static PieceState[] Pieces(int count)=>new PieceState[count];
        public static CarrierState[] Carriers(int count)=>new CarrierState[count];
        public static List<int> IntList()=>new List<int>();
        // Snapshot copying is DTO plumbing. Keep per-fragment copies on this
        // side of the bridge while Lua still decides when/how to restore them.
        public static PieceState[] CopyPieces(List<PieceState> source)
        {
            var copy=new PieceState[source.Count];for(int i=0;i<copy.Length;i++)copy[i]=source[i].Copy();return copy;
        }
        public static CarrierState[] CopyCarriers(List<CarrierState> source)
        {
            var copy=new CarrierState[source.Count];for(int i=0;i<copy.Length;i++)copy[i]=source[i].Copy();return copy;
        }
        public static void RestorePieces(List<PieceState> target,PieceState[] source)
        {target.Clear();foreach(var piece in source)target.Add(piece.Copy());}
        public static void RestoreCarriers(List<CarrierState> target,CarrierState[] source)
        {target.Clear();foreach(var carrier in source)target.Add(carrier.Copy());}
    }
}

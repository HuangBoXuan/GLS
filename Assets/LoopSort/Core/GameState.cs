using System;
using System.Collections.Generic;

namespace LoopSort0.Core
{
    // Stable C# ABI for Unity views. All gameplay decisions live in game/rules.lua.
    public sealed class GameState:IDisposable
    {
        public const int PiecesPerGroup=18,CarrierCapacity=4*PiecesPerGroup;
        public static Func<RuleStateData,IGameRuleEngine> EngineFactory;
        public RuleStateData Data {get;}=new RuleStateData();
        IGameRuleEngine engine;
        public FragmentLayout Fragments=>Data.fragments;
        public int GroupSize=>Fragments.Count;
        public int TruckCapacity=>4*GroupSize;
        public int CarrierCapacityAt(int index)=>Math.Max(1,Carriers[index].capacityGroups)*GroupSize;
        public List<PieceState> Pieces=>Data.pieces;
        public List<CarrierState> Carriers=>Data.carriers;
        public int BeltCapacity=>Data.beltCapacity;
        public RunStatus Status=>Data.status;
        public FailReason FailReason=>Data.failReason;
        public float ConveyorSpeedScale=>Data.conveyorSpeedScale;
        public int BeltCount=>Data.beltCount;
        public int FreeBeltSlots=>Math.Max(0,BeltCapacity/GroupSize-Data.beltOccupiedSlots);
        public int PendingTransfers=>Data.pendingTransfers;
        public int PackedCount=>Data.packedCount;
        public int TotalPieces=>Pieces.Count;
        public int Epoch=>Data.epoch;
        public int FeatureRevision=>Data.featureRevision;
        public sealed class KeyCollection {public int carrier,slot,color;}
        public List<KeyCollection> KeyCollections=>Data.keyCollections;
        public GameState(LevelData level,bool allowBasicRulesFallback=false)
        {
            if(EngineFactory==null)throw new InvalidOperationException("Lua must be initialized before creating game state.");
            engine=EngineFactory(Data);
            try {engine.Invoke("initialize",level,allowBasicRulesFallback);}
            catch {engine.Dispose();engine=null;throw;}
        }
        T Call<T>(string name,params object[] args)
        {
            if(engine==null)throw new ObjectDisposedException(nameof(GameState));
            object value=engine.Invoke(name,args);
            return value is T typed?typed:(T)Convert.ChangeType(value,typeof(T));
        }
        void Call(string name,params object[] args)
        {
            if(engine==null)throw new ObjectDisposedException(nameof(GameState));engine.Invoke(name,args);
        }
        public CarrierState AddCarrier(string id)=>Call<CarrierState>("add_carrier",id);
        public CarrierState AddCarrierFromData(CarrierData source)=>Call<CarrierState>("add_carrier_data",source);
        public int ReleasableCount(int index)=>engine.InvokeInt("releasable_count",index);
        public ReleaseResult Release(int index)=>Call<ReleaseResult>("release",index);
        public bool EnterBelt(int id,int epoch)=>engine.InvokeBool("enter_belt",id,epoch);
        public bool CanReceive(int carrier,int color)=>engine.InvokeBool("can_receive",carrier,color);
        public int PreferredReceiver(int color)=>engine.InvokeInt("preferred_receiver",color);
        public int ReceivePriority(int carrier,int color)=>engine.InvokeInt("receive_priority",carrier,color);
        public bool CanReceivePiece(int carrier,int id)=>engine.InvokeBool("can_receive_piece",carrier,id);
        public int ReceivePriorityForPiece(int carrier,int id)=>engine.InvokeInt("receive_priority_for_piece",carrier,id);
        public bool ReserveReceive(int id,int carrier)=>engine.InvokeBool("reserve_receive",id,carrier);
        public bool FinishReceive(int id,int epoch)=>engine.InvokeBool("finish_receive",id,epoch);
        public void Evaluate()=>Call("evaluate");
        public void Tick(float delta,bool hold=false)=>engine.Tick(delta,hold);
        public void ExpandBelt()=>Call("expand_belt");
        public bool Revive()=>Call<bool>("revive");
        public bool Shuffle(int carrier,Random random)=>Call<bool>("shuffle",carrier,random);
        public StateSnapshot Capture()=>Call<StateSnapshot>("capture");
        public void Restore(StateSnapshot snapshot)=>Call("restore",snapshot);
        public void AssertConservation()=>Call("assert_conservation");
        public void Dispose(){engine?.Dispose();engine=null;}
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        const int PhysicsPreparationsPerFrame=6;
        bool[] blockedReceivers=Array.Empty<bool>();
        readonly Dictionary<int,int> stepReceivers=new Dictionary<int,int>();
        struct CargoFlight
        {
            public PieceView view;
            public Vector3 from,to;
            public Quaternion fromRotation,toRotation;
            public int carrier,slot,token,epoch,version;
            public float elapsed;
            public bool receiving;
        }
        readonly List<CargoFlight> cargoFlights=new List<CargoFlight>(256);
        void AddBeltView(PieceView view)
        {
            if(view.BeltIndex>=0)return;
            view.BeltIndex=beltViews.Count;beltViews.Add(view);
        }
        void RemoveBeltView(PieceView view)
        {
            int index=view.BeltIndex;if(index<0)return;
            int last=beltViews.Count-1;
            if(index!=last)
            {
                var replacement=beltViews[last];beltViews[index]=replacement;replacement.BeltIndex=index;
            }
            beltViews.RemoveAt(last);view.BeltIndex=-1;
        }
        void FixedUpdate()
        {
            if(!bootComplete||Paused||State==null||State.Status!=RunStatus.Playing||beltViews.Count==0)return;
            float pieceSize=BeltPieceSize,length=track.Length,now=Time.time;
            float lapMargin=HotUpdate.RuntimeTuning.Get("receive.lap_margin",1.2f);
            float receiveRadius=HotUpdate.RuntimeTuning.Get("receive.radius",1.15f);
            float receiveRadiusSquared=receiveRadius*receiveRadius;
            float conveyorSpeed=NativeConveyorSpeed;
            float conveyorForce=BeltSteering.Force*(conveyorSpeed<0?BeltSteering.Speed:conveyorSpeed);
            stepReceivers.Clear();
            // Animation gates do not change during a physics step. Resolve them
            // once per truck, rather than for every fragment/truck pair.
            if(blockedReceivers.Length<carriers.Count)blockedReceivers=new bool[carriers.Count];
            for(int carrier=0;carrier<carriers.Count;carrier++)blockedReceivers[carrier]=VisualTransferBlocked(carrier);
            int index=0;
            while(index<beltViews.Count)
            {
                var view=beltViews[index];var body=view.Body;Vector3 position=body.position;
                var sample=beltRoute.GetSample(position);
                float distance=Mathf.Repeat(sample.distance+Vector3.Dot(position-sample.position,sample.forward),length);
                float advance=Mathf.Repeat(distance-view.TrackDistance+length*.5f,length)-length*.5f;
                view.Travelled=Mathf.Max(0,view.Travelled+advance);view.TrackDistance=distance;
                position=BeltSteering.StepWithForce(body,position,sample,conveyorForce);
                bool received=false;
                var piece=State.Pieces[view.Id];int receivingCarrier=piece.receiveTarget;
                if(receivingCarrier<0&&!stepReceivers.TryGetValue(piece.color,out receivingCarrier))
                    stepReceivers[piece.color]=receivingCarrier=State.PreferredReceiver(piece.color);
                // A fragment whose group is already reserved can only enter its
                // owning truck; distance/cooldown and Lua rules still decide when.
                bool near=false;
                if(receivingCarrier>=0&&now>=view.ReceiveAfter&&!blockedReceivers[receivingCarrier]&&
                    (view.LastCarrier!=receivingCarrier||view.Travelled>=length-lapMargin))
                {
                    Vector3 delta=position-gates[receivingCarrier];float height=Mathf.Abs(delta.y);delta.y=0;
                    near=height<=pieceSize+.6f&&delta.sqrMagnitude<=receiveRadiusSquared;
                }
                if(near&&State.ReserveReceive(view.Id,receivingCarrier))
                {
                    // A successful reservation can change capacity/colour eligibility.
                    // Lua remains authoritative; never carry this cache across mutations.
                    stepReceivers.Clear();
                    RemoveBeltView(view);received=true;
                    view.SetAnimatedPose(view.transform.position,view.transform.rotation);
                    QueueReceive(view,receivingCarrier);
                }
                // Swap removal moved an unprocessed view here.
                if(!received)index++;
            }
        }

        public bool TryRelease(int carrier)
        {
            if(Paused||IsEnteringLevel)return false;
            if(State==null||carrier<0||carrier>=State.Carriers.Count||State.Carriers[carrier].releasing>0)return false;
            if(!Convert.ToBoolean(HotUpdate.LuaGameRuntime.Call("game.flow","tutorial_allows_release",this,carrier)))return false;
            if(SelectingShuffle){Shuffle(carrier);return false;}
            if(VisualTransferBlocked(carrier)||featureViews[carrier].BlocksInteraction)return false;
            // Concurrent clicks share the last stable checkpoint, which never contains an orphaned flight.
            var checkpoint=State.PendingTransfers==0?Capture():null;
            var cargo=carrier>=0&&carrier<State.Carriers.Count?State.Carriers[carrier].cargo.ToArray():Array.Empty<int>();
            int wholeGroups=0;
            for(int group=0;(group+1)*State.GroupSize<=cargo.Length;group++)
            {
                bool whole=true;int start=group*State.GroupSize,color=State.Pieces[cargo[start]].color;
                for(int slot=start;slot<start+State.GroupSize;slot++)
                {
                    var piece=State.Pieces[cargo[slot]];
                    if(piece.location!=PieceLocation.Cargo||piece.color!=color){whole=false;break;}
                }
                if(whole)wholeGroups|=1<<group;
            }
            var result=State.Release(carrier);
            if(!result.accepted){featureViews[carrier].ShakeScrews();Hud.Toast(result.reason);return false;}
            if(checkpoint!=null)history.Push(checkpoint);Play("click");
            featureViews[carrier].AnimateRelease((cargo.Length-result.pieces.Length)/State.GroupSize);
            // Reserve the whole batch immediately, but reveal each fragment only when
            // its turn comes. Front layers leave before the solid material behind them.
            var ids=new int[result.pieces.Length];var slots=new int[ids.Length];int write=0;
            int firstSelected=cargo.Length-ids.Length;
            for(int group=(cargo.Length-1)/State.GroupSize;group>=firstSelected/State.GroupSize;group--)
            {
                for(int order=0;order<State.GroupSize;order++)
                {
                    int slot=group*State.GroupSize+EmissionSlot(order,carrier);
                    if(slot<firstSelected||slot>=cargo.Length)continue;
                    ids[write]=cargo[slot];slots[write]=slot;write++;
                }
            }
            var display=cargoDisplays[carrier];
            foreach(var model in display.groups){model.GetComponent<MotionRunner>()?.StopAll();model.transform.localScale=Vector3.one;model.transform.localRotation=Quaternion.identity;}
            var interrupted=new HashSet<int>(result.interruptedReceives);
            for(int i=0;i<ids.Length;i++)
            {
                var v=pieces[ids[i]];int group=slots[i]/State.GroupSize;
                if((wholeGroups&(1<<group))!=0)
                {
                    if(!display.queuedGroups[group])
                    {
                        var first=State.Pieces[cargo[group*State.GroupSize]];
                        display.queuedGroups[group]=true;display.launched[group]=0;
                        display.queuedColors[group]=first.color;display.queuedHidden[group]=first.hidden;
                        display.queuedBlocks[group]=first.id/State.GroupSize;
                    }
                    SetVisible(v.gameObject,false);
                }
                else if(interrupted.Contains(v.Id))v.SetAnimatedPose(v.transform.position,v.transform.rotation);
                else {SetVisible(v.gameObject,true);SetCargoPieceSize(v);v.SetAnimatedPose(CargoPosition(carrier,slots[i]),carriers[carrier].rotation);}
            }
            MarkCargoDirty(carrier);SetVisible(cargoDisplays[carrier].hull,false);
            StartCoroutine(Release(ids,slots,wholeGroups,carrier,run,State.Epoch));return true;
        }
        IEnumerator Release(int[] ids,int[] slots,int wholeGroups,int carrier,int token,int epoch)
        {
            var versions=ids.Select(id=>State.Pieces[id].transferVersion).ToArray();
            float nextLaunch=Time.time;
            Vector3 outlet=CarrierOutlet(carrier);
            var evenLanding=CargoEmission.LandingPoint(track,outlet,0);
            var oddLanding=CargoEmission.LandingPoint(track,outlet,1);
            for(int i=0;i<ids.Length;i++)
            {
                if(token!=run||epoch!=State.Epoch)yield break;
                if(State.Pieces[ids[i]].transferVersion!=versions[i]||State.Pieces[ids[i]].location!=PieceLocation.Releasing||State.Pieces[ids[i]].carrier!=carrier)continue;
                while(Paused||Time.time<nextLaunch)
                {
                    if(token!=run||epoch!=State.Epoch)yield break;if(Paused)nextLaunch+=Time.deltaTime;yield return null;
                }
                if(token!=run||epoch!=State.Epoch)yield break;
                if(State.Pieces[ids[i]].transferVersion!=versions[i]||State.Pieces[ids[i]].location!=PieceLocation.Releasing||State.Pieces[ids[i]].carrier!=carrier)continue;
                var display=cargoDisplays[carrier];int group=slots[i]/State.GroupSize;
                if(display.queuedGroups[group])
                {
                    display.launched[group]++;
                    if(display.launched[group]>=State.GroupSize)display.queuedGroups[group]=false;
                    MarkCargoGroupDirty(carrier,group);
                }
                var view=pieces[ids[i]];var piece=State.Pieces[view.Id];
                if((wholeGroups&(1<<group))!=0)
                    view.SetAnimatedPose(CargoPosition(carrier,slots[i]),carriers[carrier].rotation);
                SetCargoAppearance(view.Renderer,piece);
                view.gameObject.SetActive(true);
                Vector3 start=view.transform.position;
                Vector3 end=(i&1)==0?evenLanding:oddLanding;
                QueueFlight(view,start,end,carrier,false);
                nextLaunch+=CargoEmission.SecondsBetweenPieces;
            }
        }
        void QueueFlight(PieceView view,Vector3 from,Vector3 to,int carrier,bool receiving,int slot=-1)
        {
            if(receiving)SetCargoPieceSize(view);else SetPieceSize(view,BeltPieceSize);
            cargoFlights.Add(new CargoFlight {view=view,from=from,to=to,fromRotation=view.transform.rotation,
                toRotation=receiving?carriers[carrier].rotation:Quaternion.identity,carrier=carrier,slot=slot,receiving=receiving,
                token=run,epoch=State.Epoch,version=State.Pieces[view.Id].transferVersion});
        }
        void QueueReceive(PieceView view,int carrier)
        {
            // A successful reservation appends the fragment. Later reservations
            // only append, and releasing a fragment invalidates its flight.
            int slot=State.Carriers[carrier].cargo.Count-1;
            MarkCargoGroupDirty(carrier,slot/State.GroupSize);
            QueueFlight(view,view.transform.position,CargoPosition(carrier,slot),carrier,true,slot);
        }
        void UpdateCargoFlights()
        {
            if(Paused||cargoFlights.Count==0)return;
            float dt=Time.deltaTime,duration=CargoEmission.Duration;
            float jumpHeight=CargoEmission.JumpHeight;
            int epoch=State.Epoch,preparationsLeft=PhysicsPreparationsPerFrame;
            // One reusable value-type array replaces an iterator and coroutine
            // registration per grain. Version checks preserve interrupted returns.
            for(int i=cargoFlights.Count-1;i>=0;i--)
            {
                var flight=cargoFlights[i];var view=flight.view;
                bool valid=flight.token==run&&flight.epoch==epoch&&view!=null;
                if(valid)
                {
                    var piece=State.Pieces[view.Id];
                    valid=piece.transferVersion==flight.version&&piece.location==(flight.receiving?PieceLocation.Receiving:PieceLocation.Releasing);
                }
                if(valid)
                {
                    flight.elapsed+=dt;float t=CargoEmission.Progress(flight.elapsed/duration);
                    view.SetAnimatedPose(CargoEmission.JumpPosition(flight.from,flight.to,t,jumpHeight),Quaternion.Lerp(flight.fromRotation,flight.toRotation,t));
                    if(flight.elapsed<duration)
                    {
                        // Prepare shortly before landing so only a few flying
                        // fragments have kinematic bodies at the same time.
                        if(!flight.receiving&&view.Body==null&&preparationsLeft>0&&duration-flight.elapsed<=2*dt)
                        {
                            view.PreparePhysics();preparationsLeft--;
                        }
                        cargoFlights[i]=flight;continue;
                    }
                    if(flight.receiving)FinishReceiveFlight(view,flight.carrier,flight.slot,flight.epoch);
                    else FinishBeltFlight(view,flight.to,flight.carrier,flight.epoch);
                }
                int last=cargoFlights.Count-1;cargoFlights[i]=cargoFlights[last];cargoFlights.RemoveAt(last);
            }
        }
        void FinishBeltFlight(PieceView view,Vector3 to,int carrier,int epoch)
        {
            if(State.EnterBelt(view.Id,epoch))
            {
                // The release flight already selected the belt mesh and appearance.
                view.StartPhysics(to,Quaternion.identity,Vector3.zero);
                var sample=beltRoute.GetSample(to);
                view.Body.AddForce(BeltSteering.ForceDirection(to,sample.target)*BeltSteering.InitialImpulse,ForceMode.Impulse);
                view.TrackDistance=Mathf.Repeat(sample.distance+Vector3.Dot(to-sample.position,sample.forward),track.Length);view.Travelled=0;
                view.LastCarrier=carrier;view.ReceiveAfter=Time.time+HotUpdate.RuntimeTuning.Get("receive.cooldown",.25f);
                AddBeltView(view);
                if(State.Carriers[carrier].releasing==0)MarkCargoDirty(carrier);
            }
        }
        void FinishReceiveFlight(PieceView view,int carrier,int slot,int epoch)
        {
            if(!State.FinishReceive(view.Id,epoch))return;
            view.CargoCarrier=carrier;view.CargoSlot=slot;
            MarkCargoGroupDirty(carrier,slot/State.GroupSize);
            if(slot%State.GroupSize==State.GroupSize-1)Play("receive");
        }

    }
}

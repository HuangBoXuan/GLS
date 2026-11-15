using System.Collections.Generic;
using System.Linq;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        readonly Stack<Checkpoint> history = new Stack<Checkpoint>();
        sealed class Checkpoint
        {
            public StateSnapshot state; public Vector3[] positions,velocities,angularVelocities; public Quaternion[] rotations;
            public CarrierData[] placements; public float[] cooldown, trackDistance, travelled; public int[] lastCarrier;
        }
        Checkpoint Capture()
        {
            var snapshot=new Checkpoint { state=State.Capture(),positions=new Vector3[State.TotalPieces],rotations=new Quaternion[State.TotalPieces],
                velocities=new Vector3[State.TotalPieces],angularVelocities=new Vector3[State.TotalPieces],
                placements=placements.ToArray(),cooldown=new float[State.TotalPieces],lastCarrier=new int[State.TotalPieces],
                trackDistance=new float[State.TotalPieces],travelled=new float[State.TotalPieces] };
            foreach(var v in pieces.Values){snapshot.positions[v.Id]=v.transform.position;snapshot.rotations[v.Id]=v.transform.rotation;snapshot.velocities[v.Id]=v.Body==null?Vector3.zero:v.Body.velocity;snapshot.angularVelocities[v.Id]=v.Body==null?Vector3.zero:v.Body.angularVelocity;snapshot.cooldown[v.Id]=Mathf.Max(0,v.ReceiveAfter-Time.time);snapshot.lastCarrier[v.Id]=v.LastCarrier;snapshot.trackDistance[v.Id]=v.TrackDistance;snapshot.travelled[v.Id]=v.Travelled;}
            return snapshot;
        }
        void PreserveBoosterHistory()
        {
            if(history.Count==0)return;
            var currentPlacements=placements.ToArray();
            foreach(var checkpoint in history)
            {
                HotUpdate.LuaGameRuntime.Call("game.rules","rebase_booster_snapshot",checkpoint.state,State.Data);
                checkpoint.placements=currentPlacements;
            }
        }
        public bool RestoreUndoView()
        {
            if(history.Count==0||Hud.BoosterPresentationBusy)return false;
            Hud.Close();
            Hud.PlayBoosterUse("undo",()=>RestoreUndoImmediate());
            return true;
        }
        bool RestoreUndoImmediate()
        {
            if(history.Count==0)return false;

            var checkpoint=history.Pop();CancelUpdateCheck();StopAllCoroutines();run++;resultShown=false;State.Restore(checkpoint.state);
            shownKeyCollections=0;
            ClearCargoReveals();cargoFlights.Clear();
            ClearQueuedCargo();
            for(int i=carriers.Count-1;i>=checkpoint.placements.Length;i--){Destroy(carriers[i].gameObject);carriers.RemoveAt(i);cargoRoots.RemoveAt(i);cargoDisplays.RemoveAt(i);gates.RemoveAt(i);placements.RemoveAt(i);completionMarks.RemoveAt(i);curtains.RemoveAt(i);featureViews.RemoveAt(i);carrierOrigins.RemoveAt(i);}
            foreach(var motion in board.GetComponentsInChildren<MotionRunner>(true))motion.StopAll();
            foreach(var effect in board.GetComponentsInChildren<TransientEffect>(true))Destroy(effect.gameObject);
            for(int i=0;i<carriers.Count;i++)
            {
                carriers[i].gameObject.SetActive(true);carriers[i].position=carrierOrigins[i];
                curtains[i].SetRevealed(State.Carriers[i].curtainOpen,true);
                completionMarks[i].GetComponent<CompletionMotion>().SetComplete(State.Carriers[i].packed,true);
                featureViews[i].ResetAnimation();
            }
            beltViews.Clear();
            foreach(var view in pieces.Values)
            {
                view.BeltIndex=-1;
                view.SetAnimatedPose(checkpoint.positions[view.Id],checkpoint.rotations[view.Id]);
                var piece=State.Pieces[view.Id];SetCargoAppearance(view.Renderer,piece);
                bool belt=State.Pieces[view.Id].location==PieceLocation.Belt;if(belt)SetPieceSize(view,BeltPieceSize);else SetCargoPieceSize(view);view.gameObject.SetActive(belt);view.LastCarrier=checkpoint.lastCarrier[view.Id];view.ReceiveAfter=Time.time+checkpoint.cooldown[view.Id];
                view.TrackDistance=checkpoint.trackDistance[view.Id];view.Travelled=checkpoint.travelled[view.Id];
                if(belt){view.PausedVelocity=checkpoint.velocities[view.Id];view.PausedAngularVelocity=checkpoint.angularVelocities[view.Id];view.StartPhysics(checkpoint.positions[view.Id],checkpoint.rotations[view.Id],checkpoint.velocities[view.Id]);view.Body.angularVelocity=checkpoint.angularVelocities[view.Id];AddBeltView(view);}
            }
            foreach(var feature in groupFeatures)feature.Refresh(State,true);
            SetPaused(false);Hud.Close();RefreshCargo();UpdateBoardBounds();FrameBoard();
#if UNITY_ASSERTIONS
            State.AssertConservation();
#endif
            return true;
        }
    }
}

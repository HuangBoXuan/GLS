using System.Collections.Generic;
using LoopSort0.Core;
using UnityEngine;

namespace LoopSort0
{
    public sealed partial class GameController
    {
        const float CargoRevealDuration=1f;
        sealed class CargoCover
        {
            public bool hidden;
            public float revealedAt=-1;
        }
        struct RevealingCargo
        {
            public int block,color;
        }
        readonly Dictionary<int,CargoCover> cargoCovers=new Dictionary<int,CargoCover>();
        readonly Dictionary<Renderer,RevealingCargo> revealingCargo=new Dictionary<Renderer,RevealingCargo>();
        readonly List<Renderer> finishedCargoReveals=new List<Renderer>();
        MaterialPropertyBlock cargoRevealProperties;
        static readonly int RevealProperty=Shader.PropertyToID("_Reveal");
        static readonly int RevealedColorProperty=Shader.PropertyToID("_CargoColor");

        void ClearCargoReveals()
        {
            cargoCovers.Clear();revealingCargo.Clear();finishedCargoReveals.Clear();
        }
        void SetCargoAppearance(Renderer renderer,PieceState piece)
            =>SetCargoAppearance(renderer,piece.id/State.GroupSize,piece.color,piece.hidden);
        void SetCargoAppearance(Renderer renderer,int block,int color,bool covered)
        {
            if(!cargoCovers.TryGetValue(block,out var cover))
                cargoCovers[block]=cover=new CargoCover {hidden=covered};
            if(covered){cover.hidden=true;cover.revealedAt=-1;}
            else if(cover.hidden)
            {
                cover.hidden=false;cover.revealedAt=hiddenFlowTime;
                RecordActivity("reveal",1);
                TransientEffect.HiddenReveal(board,renderer.bounds.center,renderer.transform.rotation);
            }
            float progress=cover.revealedAt<0?1:Mathf.Clamp01((hiddenFlowTime-cover.revealedAt)/CargoRevealDuration);
            if(!covered&&progress<1)
            {
                // Track the original block, so whole models, peel remainders and
                // moving fragments share a timeline even during a rapid release.
                revealingCargo[renderer]=new RevealingCargo {block=block,color=color};
                DrawCargoReveal(renderer,color,progress);
            }
            else
            {
                revealingCargo.Remove(renderer);
                GameAssets.SetCargoAppearance(renderer,color,covered?hidden:null);
            }
        }
        void DrawCargoReveal(Renderer renderer,int color,float progress)
        {
            if(renderer.sharedMaterial!=hidden)renderer.sharedMaterial=hidden;
            if(cargoRevealProperties==null)cargoRevealProperties=new MaterialPropertyBlock();
            cargoRevealProperties.Clear();
            // Native HandleDissolve uses one second of InOutSine (0 -> 0.5
            // cutoff); this shader expresses the same interval as normalized reveal.
            cargoRevealProperties.SetFloat(RevealProperty,.5f*(1-Mathf.Cos(Mathf.PI*progress)));
            cargoRevealProperties.SetColor(RevealedColorProperty,GameAssets.Color(color));
            renderer.SetPropertyBlock(cargoRevealProperties);
        }
        void UpdateCargoReveals()
        {
            foreach(var entry in revealingCargo)
            {
                var renderer=entry.Key;var visual=entry.Value;
                if(renderer==null||!cargoCovers.TryGetValue(visual.block,out var cover))
                {finishedCargoReveals.Add(renderer);continue;}
                float progress=Mathf.Clamp01((hiddenFlowTime-cover.revealedAt)/CargoRevealDuration);
                if(progress>=1)
                {
                    GameAssets.SetCargoAppearance(renderer,visual.color);finishedCargoReveals.Add(renderer);
                }
                else if(renderer.gameObject.activeInHierarchy)DrawCargoReveal(renderer,visual.color,progress);
            }
            foreach(var renderer in finishedCargoReveals)revealingCargo.Remove(renderer);
            finishedCargoReveals.Clear();
        }
    }
}

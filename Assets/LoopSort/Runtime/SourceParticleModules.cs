using Newtonsoft.Json.Linq;
using UnityEngine;
using static LoopSort0.SourceParticleEffect;

namespace LoopSort0
{
    internal static class SourceParticleModules
    {
        internal static void Apply(ParticleSystem system,JToken source)
        {
            var initial=source["InitialModule"];var main=system.main;
            main.playOnAwake=false;main.loop=Bool(source,"looping");main.prewarm=Bool(source,"prewarm");
            main.duration=Float(source,"lengthInSec",1);main.simulationSpeed=Float(source,"simulationSpeed",1);
            main.useUnscaledTime=Bool(source,"useUnscaledTime");
            // The serialized flag is move-with-transform, not the public enum's ordering.
            main.simulationSpace=Int(source,"moveWithTransform")==0?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
            main.scalingMode=(ParticleSystemScalingMode)Int(source,"scalingMode");
            main.stopAction=ParticleSystemStopAction.None;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            main.startDelay=Curve(source["startDelay"]);
            main.startLifetime=Curve(initial["startLifetime"]);main.startSpeed=Curve(initial["startSpeed"]);
            main.startColor=Gradient(initial["startColor"]);main.gravityModifier=Curve(initial["gravityModifier"]);
            main.gravitySource=(ParticleSystemGravitySource)Int(initial,"gravitySource");
            main.emitterVelocityMode=(ParticleSystemEmitterVelocityMode)Int(source,"emitterVelocityMode");
            main.emitterVelocity=Vector(initial["customEmitterVelocity"]);
            main.startSize3D=Bool(initial,"size3D");
            if(main.startSize3D){main.startSizeX=Curve(initial["startSize"]);main.startSizeY=Curve(initial["startSizeY"]);main.startSizeZ=Curve(initial["startSizeZ"]);}
            else main.startSize=Curve(initial["startSize"]);
            main.startRotation3D=Bool(initial,"rotation3D");
            if(main.startRotation3D){main.startRotationX=Curve(initial["startRotationX"]);main.startRotationY=Curve(initial["startRotationY"]);main.startRotationZ=Curve(initial["startRotation"]);}
            else main.startRotation=Curve(initial["startRotation"]);
            main.flipRotation=Float(initial,"randomizeRotationDirection");
            main.maxParticles=Int(initial,"maxNumParticles",1000);
            system.useAutoRandomSeed=Bool(source,"autoRandomSeed",true);
            if(!system.useAutoRandomSeed)system.randomSeed=(uint)Int(source,"randomSeed");

            JToken data=source["EmissionModule"];var emission=system.emission;emission.enabled=data!=null;
            if(data!=null)
            {
                emission.rateOverTime=Curve(data["rateOverTime"]);emission.rateOverDistance=Curve(data["rateOverDistance"]);
                var bursts=(JArray)data["m_Bursts"];var values=new ParticleSystem.Burst[bursts?.Count??0];
                for(int i=0;i<values.Length;i++)
                {
                    var b=bursts[i];
                    values[i]=new ParticleSystem.Burst(Float(b,"time"),Curve(b["countCurve"])){
                        cycleCount=Int(b,"cycleCount",1),repeatInterval=Float(b,"repeatInterval",.01f),probability=Float(b,"probability",1)};
                }
                emission.SetBursts(values);
            }
            data=source["ShapeModule"];var shape=system.shape;shape.enabled=data!=null;
            if(data!=null)
            {
                shape.shapeType=(ParticleSystemShapeType)Int(data,"type");shape.angle=Float(data,"angle");
                shape.length=Float(data,"length");shape.radius=Float(data["radius"],"value");
                shape.radiusThickness=Float(data,"radiusThickness",1);shape.donutRadius=Float(data,"donutRadius");
                shape.radiusMode=(ParticleSystemShapeMultiModeValue)Int(data["radius"],"mode");
                shape.radiusSpread=Float(data["radius"],"spread");shape.radiusSpeed=Curve(data["radius"]?["speed"]);
                shape.arc=Float(data["arc"],"value",360);shape.arcMode=(ParticleSystemShapeMultiModeValue)Int(data["arc"],"mode");
                shape.arcSpread=Float(data["arc"],"spread");shape.arcSpeed=Curve(data["arc"]?["speed"]);
                shape.position=Vector(data["m_Position"]);shape.rotation=Vector(data["m_Rotation"]);shape.scale=Vector(data["m_Scale"]);
                shape.boxThickness=Vector(data["boxThickness"]);shape.alignToDirection=Bool(data,"alignToDirection");
                shape.randomDirectionAmount=Float(data,"randomDirectionAmount");shape.sphericalDirectionAmount=Float(data,"sphericalDirectionAmount");
                shape.randomPositionAmount=Float(data,"randomPositionAmount");
            }
            data=source["SizeModule"];var size=system.sizeOverLifetime;size.enabled=data!=null;
            if(data!=null)
            {
                size.separateAxes=Bool(data,"separateAxes");
                if(size.separateAxes){size.x=Curve(data["curve"]);size.y=Curve(data["y"]);size.z=Curve(data["z"]);}
                else size.size=Curve(data["curve"]);
            }
            data=source["RotationModule"];var rotation=system.rotationOverLifetime;rotation.enabled=data!=null;
            if(data!=null){rotation.separateAxes=Bool(data,"separateAxes");rotation.x=Curve(data["x"]);rotation.y=Curve(data["y"]);rotation.z=Curve(data["curve"]);}
            data=source["ColorModule"];var color=system.colorOverLifetime;color.enabled=data!=null;
            if(data!=null)color.color=Gradient(data["gradient"]);

            data=source["VelocityModule"];var velocity=system.velocityOverLifetime;velocity.enabled=data!=null;
            if(data!=null)
            {
                velocity.space=Bool(data,"inWorldSpace")?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                velocity.x=Curve(data["x"]);velocity.y=Curve(data["y"]);velocity.z=Curve(data["z"]);
                velocity.orbitalX=Curve(data["orbitalX"]);velocity.orbitalY=Curve(data["orbitalY"]);velocity.orbitalZ=Curve(data["orbitalZ"]);
                velocity.orbitalOffsetX=Curve(data["orbitalOffsetX"]);velocity.orbitalOffsetY=Curve(data["orbitalOffsetY"]);velocity.orbitalOffsetZ=Curve(data["orbitalOffsetZ"]);
                velocity.radial=Curve(data["radial"]);velocity.speedModifier=Curve(data["speedModifier"]);
            }
            data=source["ClampVelocityModule"];var limit=system.limitVelocityOverLifetime;limit.enabled=data!=null;
            if(data!=null)
            {
                limit.separateAxes=Bool(data,"separateAxis");
                limit.space=Bool(data,"inWorldSpace")?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                limit.limit=Curve(data["magnitude"]);limit.limitX=Curve(data["x"]);limit.limitY=Curve(data["y"]);limit.limitZ=Curve(data["z"]);
                limit.dampen=Float(data,"dampen");limit.drag=Curve(data["drag"]);
                limit.multiplyDragByParticleSize=Bool(data,"multiplyDragByParticleSize");
                limit.multiplyDragByParticleVelocity=Bool(data,"multiplyDragByParticleVelocity");
            }
            data=source["ForceModule"];var force=system.forceOverLifetime;force.enabled=data!=null;
            if(data!=null)
            {
                force.x=Curve(data["x"]);force.y=Curve(data["y"]);force.z=Curve(data["z"]);
                force.space=Bool(data,"inWorldSpace")?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                force.randomized=Bool(data,"randomizePerFrame");
            }
            data=source["RotationBySpeedModule"];var spin=system.rotationBySpeed;spin.enabled=data!=null;
            if(data!=null)
            {
                spin.separateAxes=Bool(data,"separateAxes");spin.x=Curve(data["x"]);spin.y=Curve(data["y"]);spin.z=Curve(data["curve"]);
                spin.range=new Vector2(Float(data["range"],"x"),Float(data["range"],"y"));
            }
            data=source["UVModule"];var uv=system.textureSheetAnimation;uv.enabled=data!=null;
            if(data!=null)
            {
                uv.mode=(ParticleSystemAnimationMode)Int(data,"mode");uv.timeMode=(ParticleSystemAnimationTimeMode)Int(data,"timeMode");
                uv.fps=Float(data,"fps",30);uv.numTilesX=Int(data,"tilesX",1);uv.numTilesY=Int(data,"tilesY",1);
                uv.animation=(ParticleSystemAnimationType)Int(data,"animationType");uv.rowMode=(ParticleSystemAnimationRowMode)Int(data,"rowMode");
                uv.rowIndex=Int(data,"rowIndex");uv.cycleCount=Int(data,"cycles",1);
                uv.frameOverTime=Curve(data["frameOverTime"]);uv.startFrame=Curve(data["startFrame"]);
                uv.speedRange=new Vector2(Float(data["speedRange"],"x"),Float(data["speedRange"],"y",1));
                uv.uvChannelMask=(UnityEngine.Rendering.UVChannelFlags)Int(data,"uvChannelMask",-1);
            }
            data=source["NoiseModule"];var noise=system.noise;noise.enabled=data!=null;
            if(data!=null)
            {
                noise.separateAxes=Bool(data,"separateAxes");
                if(noise.separateAxes){noise.strengthX=Curve(data["strength"]);noise.strengthY=Curve(data["strengthY"]);noise.strengthZ=Curve(data["strengthZ"]);}
                else noise.strength=Curve(data["strength"]);
                noise.frequency=Float(data,"frequency");noise.damping=Bool(data,"damping");
                noise.octaveCount=Int(data,"octaves",1);noise.octaveMultiplier=Float(data,"octaveMultiplier");noise.octaveScale=Float(data,"octaveScale");
                noise.quality=(ParticleSystemNoiseQuality)Int(data,"quality");noise.scrollSpeed=Curve(data["scrollSpeed"]);
                noise.remapEnabled=Bool(data,"remapEnabled");
                if(noise.separateAxes){noise.remapX=Curve(data["remap"]);noise.remapY=Curve(data["remapY"]);noise.remapZ=Curve(data["remapZ"]);}
                else noise.remap=Curve(data["remap"]);
                noise.positionAmount=Curve(data["positionAmount"]);noise.rotationAmount=Curve(data["rotationAmount"]);noise.sizeAmount=Curve(data["sizeAmount"]);
            }
            data=source["TrailModule"];var trail=system.trails;trail.enabled=data!=null;
            if(data!=null)
            {
                trail.mode=(ParticleSystemTrailMode)Int(data,"mode");trail.ratio=Float(data,"ratio",1);trail.lifetime=Curve(data["lifetime"]);
                trail.minVertexDistance=Float(data,"minVertexDistance",.1f);trail.textureMode=(ParticleSystemTrailTextureMode)Int(data,"textureMode");
                trail.worldSpace=Bool(data,"worldSpace");trail.dieWithParticles=Bool(data,"dieWithParticles");
                trail.sizeAffectsWidth=Bool(data,"sizeAffectsWidth");trail.sizeAffectsLifetime=Bool(data,"sizeAffectsLifetime");
                trail.inheritParticleColor=Bool(data,"inheritParticleColor");trail.generateLightingData=Bool(data,"generateLightingData");
                trail.colorOverLifetime=Gradient(data["colorOverLifetime"]);trail.widthOverTrail=Curve(data["widthOverTrail"]);
                trail.colorOverTrail=Gradient(data["colorOverTrail"]);trail.ribbonCount=Int(data,"ribbonCount",1);
                trail.shadowBias=Float(data,"shadowBias");trail.splitSubEmitterRibbons=Bool(data,"splitSubEmitterRibbons");
                trail.attachRibbonsToTransform=Bool(data,"attachRibbonsToTransform");
                trail.textureScale=new Vector2(Float(data["textureScale"],"x",1),Float(data["textureScale"],"y",1));
            }
            data=source["InheritVelocityModule"];var inherit=system.inheritVelocity;inherit.enabled=data!=null;
            if(data!=null){inherit.mode=(ParticleSystemInheritVelocityMode)Int(data,"m_Mode");inherit.curve=Curve(data["m_Curve"]);}
        }

        static ParticleSystem.MinMaxCurve Curve(JToken data)
        {
            if(data==null)return new ParticleSystem.MinMaxCurve(0);
            switch(Int(data,"minMaxState"))
            {
                case 1:return new ParticleSystem.MinMaxCurve(Float(data,"scalar",1),Animation(data["maxCurve"]));
                case 2:return new ParticleSystem.MinMaxCurve(Float(data,"scalar",1),Animation(data["minCurve"]),Animation(data["maxCurve"]));
                case 3:return new ParticleSystem.MinMaxCurve(Float(data,"minScalar"),Float(data,"scalar"));
                default:return new ParticleSystem.MinMaxCurve(Float(data,"scalar"));
            }
        }
        static AnimationCurve Animation(JToken data)
        {
            var keys=data?["m_Curve"] as JArray;
            var result=new Keyframe[keys?.Count??0];
            for(int i=0;i<result.Length;i++)
            {
                var key=keys[i];result[i]=new Keyframe(Float(key,"time"),Float(key,"value"),Float(key,"inSlope"),Float(key,"outSlope"),
                    Float(key,"inWeight",1f/3),Float(key,"outWeight",1f/3)){weightedMode=(WeightedMode)Int(key,"weightedMode")};
            }
            return new AnimationCurve(result){preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever};
        }
        static ParticleSystem.MinMaxGradient Gradient(JToken data)
        {
            if(data==null)return new ParticleSystem.MinMaxGradient(UnityEngine.Color.white);
            switch(Int(data,"minMaxState"))
            {
                case 1:return new ParticleSystem.MinMaxGradient(ColorGradient(data["maxGradient"]));
                case 2:return new ParticleSystem.MinMaxGradient(Color(data["minColor"]),Color(data["maxColor"]));
                case 3:return new ParticleSystem.MinMaxGradient(ColorGradient(data["minGradient"]),ColorGradient(data["maxGradient"]));
                case 4:return new ParticleSystem.MinMaxGradient(ColorGradient(data["maxGradient"])){mode=ParticleSystemGradientMode.RandomColor};
                default:return new ParticleSystem.MinMaxGradient(Color(data["maxColor"]));
            }
        }
        static UnityEngine.Gradient ColorGradient(JToken data)
        {
            var colors=new GradientColorKey[Int(data,"m_NumColorKeys",2)];
            var alpha=new GradientAlphaKey[Int(data,"m_NumAlphaKeys",2)];
            for(int i=0;i<colors.Length;i++)colors[i]=new GradientColorKey(Color(data["key"+i]),Float(data,"ctime"+i)/65535);
            for(int i=0;i<alpha.Length;i++)alpha[i]=new GradientAlphaKey(Float(data["key"+i],"a",1),Float(data,"atime"+i)/65535);
            var result=new UnityEngine.Gradient{mode=(GradientMode)Int(data,"m_Mode")};result.SetKeys(colors,alpha);return result;
        }
    }
}

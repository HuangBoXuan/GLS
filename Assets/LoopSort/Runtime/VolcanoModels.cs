using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LoopSort0
{
    // Meshes use the old art node's XY footprint so the existing shutter,
    // curtain and long-cart animation contracts also work with solid geometry.
    public static partial class VolcanoModels
    {
        public sealed class Model { public Mesh mesh; public string[] surfaces; }
        static readonly Dictionary<string,Model> models = new Dictionary<string,Model>();
        public static Material Surface(string id)
        {
            if(id=="crust-art"||id=="gate-art"||id=="blanket-art"||id=="hopper-art"||id=="lid-art"||id=="cave-art"||id=="barrier-art")
            {
                string art=id.Substring(0,id.Length-4);
                // Upright stones must retain the guide's painted lighting, too.
                // The optional crystal tint stays disabled on the cave material.
                string shaderPath="LoopSort/Shaders/"+(art=="lid"||art=="cave"?"IllustratedLid":"IllustratedRock");
                var rockShader=Application.isPlaying?HotUpdate.HotContent.Load<Shader>(shaderPath):Resources.Load<Shader>(shaderPath);
                var paint=Application.isPlaying?HotUpdate.HotContent.Texture("LoopSort/Theme/mine-"+art):Resources.Load<Texture2D>("LoopSort/Theme/mine-"+art);
                if(rockShader==null||paint==null)throw new InvalidOperationException("Missing illustrated mechanism material assets");
                return new Material(rockShader){name="Volcano 3D / "+art+" guide paint",mainTexture=paint,enableInstancing=true};
            }
            var shader = Application.isPlaying ? HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/VolcanoSurface") : Resources.Load<Shader>("LoopSort/Shaders/VolcanoSurface");
            if (shader == null) throw new InvalidOperationException("Missing volcanic mesh surface shader");
            uint rgb; float tint = 0, emission = 0, fabric = 0, pattern = 0, polish=.52f,metal=0;
            switch (id)
            {
                case "iron": rgb=0x69608C; polish=.61f;metal=.65f;break;
                case "mechanism-trim": rgb=0xA89BC5; polish=.84f;metal=.67f;break;
                case "bolt-face": rgb=0xCBBAD8; polish=.91f;metal=.71f;break;
                case "warning": rgb=0xFFC13E; polish=.66f;break;
                case "ore-emblem": rgb=0xCBEEFF; break;
                case "body-iron": rgb=0x504768; tint=1;polish=.39f;break;
                case "body-trim": rgb=0xC4BDD6; tint=1;polish=.68f;metal=.56f;break;
                case "body-dark": rgb=0x3F3455; tint=.29f;polish=.26f;break;
                case "lid-metal": rgb=0x2054CF; polish=.75f;break;
                case "lid-accent": rgb=0xCA2859; tint=1;polish=.67f;break;
                case "rock": rgb=0x464878; break;
                case "rock-light": rgb=0x686DAB; break;
                case "dark": rgb=0x322647; polish=.18f;break;
                case "copper": rgb=0x795E95; polish=.74f;metal=.72f;break;
                case "gold": rgb=0xFFAD32; polish=.84f;metal=.42f;break;
                case "lamp-glass": rgb=0xFFD16A; emission=1; break;
                case "lamp-glow": rgb=0xFFF1B0; emission=1; break;
                case "cream": rgb=0xD5C8FF; break;
                case "lava": rgb=0xAE49C7; emission=.65f; break;
                case "lava-core": rgb=0xF284DD; emission=.8f; break;
                case "color": rgb=0x798CE0; tint=.9f; break;
                case "fabric": rgb=0x426CD4; fabric=1;polish=0;break;
                case "sugar-blue": rgb=0x3574E8; polish=.72f;break;
                case "sugar-purple": rgb=0x806BA2; break;
                case "sugar-pink": rgb=0xDC2364; polish=.84f;break;
                case "glaze-blue": rgb=0x464A8C; polish=.82f;break;
                case "glaze-indigo": rgb=0x37346E; polish=.82f;break;
                case "chain-metal": rgb=0x74648C; polish=.89f;metal=.7f;break;
                case "barrier-drum": rgb=0x513C67; polish=.79f;metal=.28f;break;
                case "frost": rgb=0xE6DDEF; break;
                case "chocolate": rgb=0x6F3422; polish=.58f;break;
                case "chocolate-dark": rgb=0x442018; polish=.13f;break;
                case "chocolate-gloss": rgb=0x874630; polish=.72f;break;
                case "gate-lock": rgb=0xEAAF4D; tint=.88f; break;
                case "peppermint": rgb=0xFFE2C6; tint=.27f; break;
                case "hazard": rgb=0x49384D; pattern=1; break;
                case "arch-hazard": rgb=0x49384D; pattern=2; break;
                case "body-panel": rgb=0x3B5CA7; tint=1;polish=.60f;break;
                default: throw new ArgumentException("Unknown volcanic surface "+id);
            }
            var color=ArtTheme.Hex(rgb);
            var material=new Material(shader) { name="Volcano 3D / "+id, color=color, enableInstancing=true };
            material.SetColor("_BaseColor",color);material.SetFloat("_TintStrength",tint);
            material.SetFloat("_Emission",emission);material.SetFloat("_Fabric",fabric);material.SetFloat("_CandyStripe",pattern);
            material.SetFloat("_Polish",polish);material.SetFloat("_Metal",metal);
            return material;
        }
        public static void Release()
        {
            foreach(var model in models.Values)if(model.mesh!=null)
            {if(Application.isPlaying)Object.Destroy(model.mesh);else Object.DestroyImmediate(model.mesh);}
            models.Clear();
        }
        public static Model Get(string id,Vector2 size)
        {
            string key=id+"/"+size.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"/"+size.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            if(models.TryGetValue(key,out var cached)&&cached.mesh!=null)return cached;
            var b=new Builder();float w=size.x,d=size.y;
            switch(id)
            {
                case "cart": Cart(b,w,d);break;
                case "cave": Cave(b,w,d);break;
                case "crust": CandyCrust(b,w,d);break;
                case "gate": CandyGate(b,w,d);break;
                case "gate-beam": CandyGateBeams(b,w,d);break;
                case "gate-left": case "gate-right": CandyGatePost(b,w,d,id=="gate-left"?-1f:1f);break;
                case "lid": Lid(b,w,d);break;
                case "barrier": Barrier(b,w,d);break;
                case "blanket": CandyBlanket(b,w,d);break;
                case "blanket-roller": CandyBlanketRoller(b,w,d);break;
                case "hopper-frame": CandyHopperFrame(b,w,d);break;
                case "hopper-left": case "hopper-right": HopperDoor(b,w,d,id=="hopper-right");break;
                case "gate-post": Post(b,w,d);break;
                case "hopper-body": HopperBody(b,w,d);break;
                case "hopper-top": case "hopper-bottom": case "hopper-rim-left": case "hopper-rim-right": Housing(b,w,d);break;
                case "chain": Chain(b,w,d);break;
                case "key": Key(b,w,d);break;
                case "lock": Lock(b,w,d);break;
                case "tag": Tag(b,w,d);break;
                case "connector": Connector(b,w,d);break;
                case "blanket-rails": BlanketRails(b,w,d);break;
                case "blanket-edge": BlanketEdge(b,w,d);break;
                case "blanket-clasp": BlanketClasp(b,w,d);break;
                default: throw new ArgumentException("No solid model for "+id);
            }
            var result=b.Finish("Volcano 3D / "+id,w,d,id=="cave",CandyFootprint(id));models[key]=result;return result;
        }
        static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
        static void Cart(Builder b,float w,float d)
        {
            float half=w*.405f,end=d*.475f;
            b.Box("body-dark",P(0,-.08f,0),P(w*.8f,.25f,d*.96f),.1f);
            b.Box("body-iron",P(0,-.10f,0),P(w*.68f,.09f,d*.88f),.06f);
            foreach(float side in new[]{-1f,1f})
            {
                b.Box("body-iron",P(side*(half-.077f),.66f,0),P(w*.105f,1.55f,d*.96f),.087f,
                    Quaternion.Euler(0,0,-side*7f));
                b.Box("body-trim",P(side*half,1.44f,0),P(w*.115f,.14f,d*.96f),.045f);
                var tilt=Quaternion.Euler(0,0,-side*7f);
                var wall=P(side*(half-.077f),.66f,0);
                b.RoundBox("body-panel",wall+tilt*P(side*w*.054f,-.20f,0),P(.057f,.64f,d*.91f),.047f,tilt);
                b.Box("hazard",wall+tilt*P(side*w*.058f,.27f,0),P(.029f,.31f,d*.84f),.012f,tilt);
                // Only the closed rear corners carry top fasteners.
                b.Box("copper",P(side*half,1.46f,end),P(w*.115f,.14f,.30f),.045f);
                b.Cylinder("gold",P(side*half,1.55f,end),.055f,.035f,Vector3.up,8);
                foreach(float z in new[]{-d*.5f+1.34f,d*.5f-1.34f})
                {
                    b.Cylinder("dark",P(side*w*.4125f,-.49f,z),.47f,w*.10f,Vector3.right,12);
                    b.Cylinder("iron",P(side*w*.4125f,-.49f,z),.44f,w*.10f,Vector3.right,12);
                    b.Cylinder("copper",P(side*w*.429f,-.49f,z),.23f,w*.067f,Vector3.right,14);
                    b.Cylinder("mechanism-trim",P(side*w*.453f,-.49f,z),.115f,w*.025f,Vector3.right,10);
                }
            }
            b.Box("body-iron",P(0,.66f,end),P(w*.84f,1.55f,.24f),.06f);
            b.Box("body-trim",P(0,1.44f,end),P(w*.86f,.14f,.28f),.04f);
            b.Box("body-dark",P(0,.54f,end+.113f),P(w*.52f,.75f,.034f),.014f);
            TailLantern(b,w,end);
            // The -Z mouth has no crossbar. Only the recessed cargo floor
            // reaches the opening; the opposite end has a full-height back.
        }
        static void TailLantern(Builder b,float width,float end)
        {
            // Raised centered mount on the closed end, above the cargo walls.
            float z=end-.085f;
            b.Box("dark",P(0,1.50f,z),P(width*.29f,.15f,.37f),.058f);
            b.Box("mechanism-trim",P(0,1.82f,z),P(width*.235f,.59f,.36f),.11f);
            b.Box("lamp-glass",P(0,2.108f,z),P(width*.141f,.035f,.115f),.015f);
            foreach(float direction in new[]{-1f,1f})
            {
                float face=z+direction*.166f;
                b.Box("dark",P(0,1.88f,face),P(width*.187f,.235f,.055f),.026f);
                for(int i=-1;i<=1;i++)
                    b.Box("lamp-glass",P(i*width*.042f,1.88f,face+direction*.026f),P(width*.030f,.155f,.028f),.012f);
            }
        }
        static void Cave(Builder b,float w,float d)
        {
            float inner=w*.325f,outer=w*.45f,spring=.99f,depth=d*.54f;
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*(inner+outer)*.5f,leg=outer-inner;
                b.RoundBox("dark",P(x,.10f,0),P(leg+.25f,.20f,depth),.072f);
                b.RoundBox("copper",P(x,.237f,0),P(leg+.21f,.24f,depth*.98f),.081f);
                b.RoundBox("mechanism-trim",P(x,.331f,0),P(leg+.14f,.095f,depth*.97f),.034f);
                b.RoundBox("iron",P(x,.648f,0),P(leg+.045f,.73f,depth*.88f),.066f);
                foreach(float front in new[]{-1f,1f})
                {
                    b.Box("arch-hazard",P(x,.637f,front*depth*.444f),P(leg*.87f,.50f,.018f),.008f);
                    b.RoundBox("dark",P(x,.232f,front*depth*.496f),P(leg*1.27f,.157f,.06f),.023f);
                    b.RoundBox("gold",P(x,.232f,front*depth*.528f),P(leg*1.07f,.112f,.026f),.016f);
                    for(int i=-1;i<=1;i++)b.Box("lamp-glow",P(x+i*leg*.26f,.232f,front*depth*.546f),P(leg*.14f,.066f,.026f),.008f);
                }
            }
            var center=P(0,spring,0);
            b.ArchTube("lid-metal",spring,(inner+outer)*.5f,(outer-inner)*.5f,depth*.49f,0,Mathf.PI);
            b.ArchTube("sugar-blue",spring,inner+.038f,.040f,depth*.52f,.04f,Mathf.PI-.04f);
            b.ArchTube("sugar-pink",spring,outer-.040f,.123f,depth*.45f,Mathf.PI*.50f,Mathf.PI*.965f);
            foreach(float angle in new[]{.13f,.5f,.87f})
            {
                float mid=angle*Mathf.PI;
                b.ArchWedge("copper",center,inner-.020f,outer+.084f,mid-.147f,mid+.147f,depth*1.11f,.050f);
                b.ArchWedge("mechanism-trim",center,inner-.019f,outer+.087f,mid-.102f,mid+.102f,depth*1.13f,.045f);
                foreach(float front in new[]{-1f,1f})
                {
                    var point=P(Mathf.Cos(mid)*(inner+outer)*.5f,spring+Mathf.Sin(mid)*(inner+outer)*.5f,front*depth*.57f);
                    b.Ellipsoid("bolt-face",point,P(.061f,.061f,.032f));
                }
            }
        }
        static void Post(Builder b,float w,float d)
        {
            b.Box("dark",P(0,0,0),P(w,.20f,d),.07f);
            for(int i=0;i<5;i++)
            {
                float z=(i-2)*d*.195f;
                b.Box(i%2==0?"iron":"rock-light",P(0,.15f,z),P(w*.95f,.33f,d*.19f),.08f);
                if(i%2==0)b.Ellipsoid("sugar-pink",P(w*.28f,.29f,z),P(w*.19f,.05f,d*.085f));
            }
            b.Box("mechanism-trim",P(-w*.18f,.26f,0),P(w*.23f,.13f,d*.85f),.045f);
        }
        static void Bolt(Builder b,Vector3 p,float radius)
        {
            b.Cylinder("dark",p-Vector3.up*.03f,radius*1.16f,.055f,Vector3.up,8);
            b.Cylinder("copper",p,radius,.085f,Vector3.up,8);
            b.Cylinder("bolt-face",p+Vector3.up*.05f,radius*.79f,.028f,Vector3.up,8);
        }
        static void Lid(Builder b,float w,float d)
        {
            b.Box("dark",P(w*.025f,0,0),P(w*.95f,.21f,d*.98f),.10f);
            b.Box("iron",P(w*.025f,.09f,0),P(w*.91f,.23f,d*.97f),.10f);
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*d*.359f;
                LidEndPlate(b,"mechanism-trim",P(w*.025f,.25f,z),w*.93f,d*.252f,.23f);
                LidEndPlate(b,"dark",P(w*.025f,.357f,z),w*.80f,d*.209f,.060f);
                LidEndPlate(b,"lid-metal",P(w*.025f,.392f,z),w*.75f,d*.184f,.066f);
                foreach(float x in new[]{-.25f,.30f})
                    b.Ellipsoid("bolt-face",P(x*w,.43f,z),P(w*.047f,.044f,w*.047f));

                var hinge=P(-w*.435f,.10f,side*d*.258f);
                b.Cylinder("copper",hinge,w*.068f,d*.144f,Vector3.forward,16);
                b.Cylinder("mechanism-trim",hinge,w*.073f,d*.107f,Vector3.forward,16);
                b.Cylinder("bolt-face",hinge+Vector3.forward*(side*d*.056f),w*.056f,.035f,Vector3.forward,16);
            }
            b.Box("copper",P(w*.025f,.229f,0),P(w*.79f,.21f,d*.46f),.10f);
            b.Box("mechanism-trim",P(w*.025f,.306f,0),P(w*.72f,.13f,d*.419f),.057f);
            b.Box("iron",P(w*.025f,.36f,0),P(w*.63f,.056f,d*.37f),.026f);
            foreach(float side in new[]{-1f,1f})foreach(float end in new[]{-1f,1f})
                b.Ellipsoid("bolt-face",P(w*(.025f+side*.267f),.39f,end*d*.156f),P(w*.037f,.038f,w*.037f));
            float radius=Mathf.Min(w*.257f,d*.152f);
            b.Cylinder("dark",P(w*.025f,.389f,0),radius*1.08f,.06f,Vector3.up,24);
            Peppermint(b,P(w*.025f,.426f,0),radius,"lid-accent","peppermint");
        }
        static void LidEndPlate(Builder b,string material,Vector3 center,float width,float depth,float height)
        {
            const int arcSegments=8;
            float radius=depth*.5f,halfStraight=width*.5f-radius;
            var outline=new Vector2[(arcSegments+1)*2];
            for(int side=0;side<2;side++)for(int step=0;step<=arcSegments;step++)
            {
                float angle=(side-.5f+step/(float)arcSegments)*Mathf.PI;
                outline[side*(arcSegments+1)+step]=new Vector2(
                    center.x+(side==0?halfStraight:-halfStraight)+Mathf.Cos(angle)*radius,
                    center.z+Mathf.Sin(angle)*radius);
            }
            b.Polygon(material,outline,1,1,center.y,height,.056f);
        }
        static void HopperDoor(Builder b,float w,float d,bool mirror)
        {
            b.Box("dark",P(0,0,0),P(w,.25f,d),.075f);
            var outline=Octagon(.5f,.5f,.16f);
            b.Polygon("copper",outline,w*.98f,d*.98f,.112f,.20f,.05f);
            b.Polygon("sugar-blue",outline,w*.86f,d*.88f,.235f,.125f,.044f);
            b.Polygon("lid-metal",outline,w*.71f,d*.74f,.295f,.034f,.015f);
            float side=mirror?-1f:1f;
            foreach(float end in new[]{-1f,1f})
                CandyCord(b,"sugar-blue",.022f,P(-side*w*.28f,.316f,end*d*.24f),
                    P(-side*w*.14f,.316f,end*d*.115f),P(side*w*.30f,.316f,end*d*.04f));
            b.RoundBox("dark",P(side*w*.288f,.327f,0),P(w*.23f,.10f,d*.30f),.040f);
            b.RoundBox("sugar-pink",P(side*w*.288f,.399f,0),P(w*.156f,.119f,d*.238f),.044f);
            b.Box("lava-core",P(side*w*.288f,.442f,-d*.035f),P(w*.083f,.022f,d*.084f),.011f);
        }
        static void HopperBody(Builder b,float w,float d)
        {
            b.Box("dark",P(0,-.03f,0),P(w,.20f,d),.07f);
            foreach(float side in new[]{-1f,1f})
            {
                b.Box("iron",P(side*(w*.5f-.09f),.85f,0),P(.18f,1.78f,d),.06f);
                b.Box("iron",P(0,.85f,side*(d*.5f-.09f)),P(w-.30f,1.78f,.18f),.06f);
            }
        }
        static void Housing(Builder b,float w,float d)
        {
            b.Box("iron",P(0,0,0),P(w,.30f,d),.065f);
            if(w>d)
            {
                foreach(float x in new[]{-.39f,.39f})
                {
                    b.Box("copper",P(x*w,.13f,0),P(d*1.25f,.27f,d*.98f),.09f);
                    b.Box("bolt-face",P(x*w,.275f,0),P(d*1.02f,.045f,d*.79f),.02f);
                    b.Box("dark",P(x*w,.32f,0),P(d*.30f,.09f,d*.30f),.028f);
                }
                b.Box("copper",P(0,.12f,0),P(w*.34f,.19f,d*.97f),.05f);
                b.Box("dark",P(0,.23f,0),P(w*.27f,.10f,d*.72f),.035f);
            }
            else b.Box("mechanism-trim",P(0,.13f,0),P(w*.58f,.08f,d*.96f),.025f);
        }
        static void Chain(Builder b,float w,float d)
        {
            float tube=Mathf.Min(.071f,w*.075f),step=.34f;
            foreach(float side in new[]{-1f,1f})
            {
                int nx=Mathf.Max(2,Mathf.CeilToInt(w/step)),nz=Mathf.Max(2,Mathf.CeilToInt(d/step));
                for(int i=1;i<nx;i++)b.Ring("chain-metal",P(w*(-.5f+(i-.5f)/(nx-1)),.08f,side*d*.5f),.234f,.092f,tube,Quaternion.Euler((i%2)*62,0,0));
                for(int i=1;i<nz;i++)b.Ring("chain-metal",P(side*w*.5f,.08f,d*(-.5f+(i-.5f)/(nz-1))),.092f,.234f,tube,Quaternion.Euler(0,0,(i%2)*62));
                foreach(float z in new[]{-d*.5f,d*.5f})b.Ring("gold",P(side*w*.5f,.136f,z),.158f,.158f,tube*1.28f,Quaternion.Euler(0,35,0));
            }
            b.Ring("gold",P(0,.164f,d*.5f),.335f,.109f,tube*1.26f,Quaternion.identity);
        }
        static void BlanketRails(Builder b,float w,float d)
        {
            foreach(float side in new[]{-1f,1f})
            {
                b.Box("dark",P(0,-.025f,side*d*.5f),P(w,.19f,.26f),.055f);
                b.Box("mechanism-trim",P(0,.055f,side*d*.5f),P(w*.99f,.12f,.21f),.04f);
            }
        }
        static void BlanketEdge(Builder b,float w,float d)
        {
            // Soft red hem at the sides; dark rigid rails only at the two ends.
            b.Box("fabric",P(0,-.02f,0),P(w*.55f,.10f,d*.97f),.035f);
            BlanketClasp(b,w,d);
        }
        static void BlanketClasp(Builder b,float w,float d)
        {
            foreach(float side in new[]{-1f,1f})
            {
                b.Box("dark",P(0,.025f,side*d*.5f),P(w*1.08f,.14f,.37f),.03f);
                b.Box("copper",P(0,.10f,side*d*.5f),P(w,.16f,.35f),.04f);
                Bolt(b,P(0,.20f,side*d*.5f),Mathf.Min(.055f,w*.23f));
            }
        }
        static void Barrier(Builder b,float w,float d)
        {
            const float height=.28f;
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*w*.415f;
                b.RoundBox("dark",P(x,.02f,0),P(w*.17f,.24f,d),.071f);
                b.RoundBox("lid-metal",P(x,.166f,0),P(w*.17f,.30f,d*.96f),.085f);
                b.RoundBox("sugar-blue",P(x,.29f,0),P(w*.138f,.155f,d*.82f),.065f);
                b.Cylinder("gold",P(side*w*.355f,height,0),d*.294f,w*.090f,Vector3.right,16);
                b.Cylinder("mechanism-trim",P(side*w*.318f,height,0),d*.316f,w*.046f,Vector3.right,16);
                foreach(float end in new[]{-1f,1f})
                    b.Ellipsoid("bolt-face",P(x,.365f,end*d*.32f),P(.041f,.035f,.041f));
            }
            b.StripedRod(w*.67f,d*.224f,height);
        }
        static void Key(Builder b,float w,float d)
        {
            float s=Mathf.Min(w,d*2.4f);
            b.Ring("dark",P(s*.28f,.015f,0),s*.20f,d*.32f,s*.065f,Quaternion.identity);
            b.Ring("color",P(s*.28f,.1f,0),s*.20f,d*.32f,s*.055f,Quaternion.identity);
            b.Box("color",P(-s*.13f,.10f,0),P(s*.62f,.16f,d*.18f),.035f);
            for(int i=0;i<2;i++)b.Box("color",P(-s*(.22f+i*.17f),.10f,-d*.15f),P(s*.09f,.16f,d*.29f),.025f);
            b.Cylinder("gold",P(-s*.06f,.21f,0),s*.035f,.025f,Vector3.up,8);
        }
        static void Lock(Builder b,float w,float d)
        {
            b.Ring("copper",P(0,.03f,d*.17f),w*.26f,d*.245f,w*.073f,Quaternion.identity);
            b.Ring("gold",P(0,.085f,d*.17f),w*.26f,d*.245f,w*.060f,Quaternion.identity);
            b.RoundBox("copper",P(0,.055f,-d*.12f),P(w*.85f,.23f,d*.54f),.092f);
            b.RoundBox("gold",P(0,.147f,-d*.12f),P(w*.79f,.174f,d*.50f),.082f);
            b.RoundBox("gate-lock",P(0,.223f,-d*.12f),P(w*.64f,.056f,d*.35f),.026f);
            b.Cylinder("chocolate-dark",P(0,.258f,-d*.08f),w*.081f,.022f,Vector3.up,12);
            b.Box("chocolate-dark",P(0,.258f,-d*.16f),P(w*.072f,.022f,d*.14f),.012f);
        }
        static void Emblem(Builder b,Vector3 center,float s)
        {
            b.Shard("cream",center,s,.83f,0,0);
            b.Shard("cream",center+P(-s*.17f,0,-s*.12f),s*.67f,.83f,-24,-.02f);
            b.Shard("cream",center+P(s*.18f,0,-s*.13f),s*.58f,.83f,25,.015f);
        }
        static Vector2[] Octagon(float x,float z,float cut)=>new[]{new Vector2(-x+cut,-z),new Vector2(x-cut,-z),new Vector2(x,-z+cut),new Vector2(x,z-cut),new Vector2(x-cut,z),new Vector2(-x+cut,z),new Vector2(-x,z-cut),new Vector2(-x,-z+cut)};
        static void Tag(Builder b,float w,float d)
        {
            b.Box("body-iron",P(0,-.10f,-d*.38f),P(w*.25f,.2f,d*.35f),.03f);
            var plate=Octagon(.5f,.5f,.16f);
            b.Polygon("copper",plate,w,d*.88f,.02f,.22f,.025f,0,.80f);
            b.Polygon("dark",plate,w*.80f,d*.704f,.04f,.08f,.015f);
            b.Polygon("iron",plate,w*.73f,d*.64f,.075f,.025f,.008f);
            foreach(float side in new[]{-1f,1f})
            {
                b.Cylinder("dark",P(side*w*.40f,.135f,0),w*.075f,.06f,Vector3.up,10);
                b.Cylinder("gold",P(side*w*.40f,.18f,0),w*.055f,.045f,Vector3.up,10);
            }
            Emblem(b,P(0,.10f,0),w*.94f);
        }
        static void Connector(Builder b,float w,float d)
        {
            float s=Mathf.Min(w,d),run=Mathf.Max(w,d)*.68f;
            Vector3 axis=P(.6f,0,.8f),cross=P(.8f,0,-.6f);
            var center=P(0,.16f,0);
            b.Cylinder("dark",center,s*.116f,run,axis,16);
            var thread=new Vector3[33];
            for(int i=0;i<thread.Length;i++)
            {
                float t=i/(float)(thread.Length-1),angle=8*Mathf.PI*t;
                thread[i]=center+axis*(run*.78f*(t-.5f))+(cross*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*(s*.114f);
            }
            CandyCord(b,"sugar-pink",s*.071f,thread);
            foreach(float side in new[]{-1f,1f})
            {
                b.Cylinder("mechanism-trim",center+axis*(run*.410f*side),s*.159f,run*.108f,axis,16);
                b.Cylinder("copper",center+axis*(run*.456f*side),s*.174f,run*.058f,axis,16);
                Vector3 end=center+axis*run*.5f*side;
                foreach(float lobe in new[]{-1f,1f})
                    b.Ellipsoid("copper",end+cross*(lobe*s*.098f),P(s*.159f,s*.147f,s*.16f),.90f);
                b.Ellipsoid("bolt-face",end+axis*(run*.027f*side)+Vector3.up*(s*.144f),P(s*.057f,s*.027f,s*.057f));
            }
        }
        sealed partial class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<string> surfaces=new List<string>();
            readonly List<List<int>> indices=new List<List<int>>();
            int Slot(string id)
            {
                int i=surfaces.IndexOf(id);if(i>=0)return i;
                surfaces.Add(id);indices.Add(new List<int>());return surfaces.Count-1;
            }
            void Face(int slot,Vector3 a,Vector3 b,Vector3 c,Vector3 expected)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),expected)<0){var swap=b;b=c;c=swap;}
                int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                uv.Add(new Vector2(a.x,a.z));uv.Add(new Vector2(b.x,b.z));uv.Add(new Vector2(c.x,c.z));
                indices[slot].Add(start);indices[slot].Add(start+1);indices[slot].Add(start+2);
            }
            void Quad(int slot,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
            {Face(slot,a,b,c,normal);Face(slot,a,c,d,normal);}
            public void StripedRod(float length,float radius,float height)
            {
                const int sides=16,bands=7;
                int dark=Slot("barrier-drum"),yellow=Slot("warning");
                Vector3 Point(int band,int side)
                {
                    float angle=2*Mathf.PI*side/sides;
                    float offset=band==0||band==bands?0:Mathf.Cos(angle)*length*.062f;
                    return P(length*(band/(float)bands-.5f)+offset,height+Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                }
                for(int band=0;band<bands;band++)for(int side=0;side<sides;side++)
                {
                    Vector3 a=Point(band,side),b=Point(band,side+1),c=Point(band+1,side+1),d=Point(band+1,side);
                    Vector3 normal=P(0,(a.y+b.y)*.5f-height,(a.z+b.z)*.5f);
                    int first=vertices.Count;
                    Quad(band%2==0?dark:yellow,a,b,c,d,normal);
                    for(int i=first;i<vertices.Count;i++)
                        curvedNormals[i]=P(0,vertices[i].y-height,vertices[i].z).normalized;
                }
                // The solid metal collars cap both ends of the striped drum.
            }
            public void Polygon(string id,Vector2[] outline,float width,float depth,float y,float height,float bevel,float facet=0,float hole=0)
            {
                int slot=Slot(id),count=outline.Length;Vector3 center=Vector3.zero;
                foreach(var p in outline)center+=P(p.x*width,y,p.y*depth);center/=count;
                var rings=new Vector3[4,2,count];float edge=Mathf.Min(bevel,height*.35f);
                for(int r=0;r<4;r++)for(int i=0;i<count;i++)
                {
                    bool cap=r==0||r==3;Vector3 delta=P(outline[i].x*width,y,outline[i].y*depth)-center;
                    float h=r==0?-height*.5f:r==1?-height*.5f+edge:r==2?height*.5f-edge:height*.5f;
                    float inset=Mathf.Min(bevel,delta.magnitude*.40f);
                    rings[r,0,i]=center+delta-(cap?delta.normalized*inset:Vector3.zero)+Vector3.up*h;
                    rings[r,1,i]=center+delta*hole+(cap?delta.normalized*bevel:Vector3.zero)+Vector3.up*h;
                }
                for(int i=0;i<count;i++)
                {
                    int j=(i+1)%count;Vector3 normal=(rings[1,0,i]+rings[1,0,j])*.5f-center;
                    for(int r=0;r<3;r++)
                    {
                        Quad(slot,rings[r,0,i],rings[r,0,j],rings[r+1,0,j],rings[r+1,0,i],normal);
                        if(hole>0)Quad(slot,rings[r,1,i],rings[r,1,j],rings[r+1,1,j],rings[r+1,1,i],-normal);
                    }
                    if(hole>0)
                    {
                        Quad(slot,rings[3,0,i],rings[3,0,j],rings[3,1,j],rings[3,1,i],Vector3.up);
                        Quad(slot,rings[0,0,i],rings[0,0,j],rings[0,1,j],rings[0,1,i],Vector3.down);
                    }
                    else
                    {
                        Vector3 peak=center+P(facet*.25f,height*.5f+facet,-facet*.4f);
                        Face(slot,peak,rings[3,0,i],rings[3,0,j],Vector3.up);
                        Face(slot,center+Vector3.down*height*.5f,rings[0,0,i],rings[0,0,j],Vector3.down);
                    }
                }
            }
            public void Shard(string id,Vector3 center,float size,float aspect,float angle,float lift)
            {
                int slot=Slot(id);Quaternion rotation=Quaternion.Euler(0,angle,0);
                var outline=new[]{new Vector2(-.10f,-.40f),new Vector2(.10f,-.40f),new Vector2(.17f,.15f),new Vector2(.025f,.50f),new Vector2(-.17f,.15f)};
                Vector3 ridge=center+rotation*P(-size*.02f,Mathf.Max(.025f,size*.14f+lift),size*.10f);
                for(int i=0;i<outline.Length;i++)
                {
                    var p=outline[i];var q=outline[(i+1)%outline.Length];
                    Vector3 a=center+rotation*P(p.x*size,0,p.y*size*aspect),c=center+rotation*P(q.x*size,0,q.y*size*aspect);
                    Face(slot,a,c,ridge,Vector3.up);
                    Quad(slot,a,c,c-Vector3.up*size*.03f,a-Vector3.up*size*.03f,(a+c)*.5f-center);
                    Face(slot,center-Vector3.up*size*.03f,a-Vector3.up*size*.03f,c-Vector3.up*size*.03f,Vector3.down);
                }
            }
            public void ArchWedge(string id,Vector3 center,float inner,float outer,float start,float end,float depth,float bevel)
            {
                int slot=Slot(id),segments=Mathf.Max(1,Mathf.CeilToInt((end-start)*18/Mathf.PI));
                var rings=new Vector3[4,2,segments+1];
                for(int ring=0;ring<4;ring++)
                {
                    bool cap=ring==0||ring==3;float inset=cap?bevel:0;
                    float angleInset=cap?Mathf.Min(.012f,(end-start)*.12f):0;
                    float z=ring==0?-depth*.5f:ring==1?-depth*.5f+bevel:ring==2?depth*.5f-bevel:depth*.5f;
                    for(int i=0;i<=segments;i++)
                    {
                        float a=Mathf.Lerp(start+angleInset,end-angleInset,i/(float)segments);
                        rings[ring,0,i]=center+P(Mathf.Cos(a)*(inner+inset),Mathf.Sin(a)*(inner+inset),z);
                        rings[ring,1,i]=center+P(Mathf.Cos(a)*(outer-inset),Mathf.Sin(a)*(outer-inset),z);
                    }
                }
                for(int i=0;i<segments;i++)
                {
                    float a=Mathf.Lerp(start,end,(i+.5f)/segments);Vector3 radial=P(Mathf.Cos(a),Mathf.Sin(a),0);
                    for(int ring=0;ring<3;ring++)
                    {
                        Quad(slot,rings[ring,0,i],rings[ring,0,i+1],rings[ring+1,0,i+1],rings[ring+1,0,i],-radial);
                        Quad(slot,rings[ring,1,i],rings[ring,1,i+1],rings[ring+1,1,i+1],rings[ring+1,1,i],radial);
                    }
                    Quad(slot,rings[0,0,i],rings[0,1,i],rings[0,1,i+1],rings[0,0,i+1],Vector3.back);
                    Quad(slot,rings[3,0,i],rings[3,1,i],rings[3,1,i+1],rings[3,0,i+1],Vector3.forward);
                }
                for(int ring=0;ring<3;ring++)
                {
                    Quad(slot,rings[ring,0,0],rings[ring,1,0],rings[ring+1,1,0],rings[ring+1,0,0],P(Mathf.Sin(start),-Mathf.Cos(start),0));
                    Quad(slot,rings[ring,0,segments],rings[ring,1,segments],rings[ring+1,1,segments],rings[ring+1,0,segments],P(-Mathf.Sin(end),Mathf.Cos(end),0));
                }
            }
            public void Box(string id,Vector3 center,Vector3 size,float bevel,Quaternion rotation=default)
            {
                if(rotation.x==0&&rotation.y==0&&rotation.z==0&&rotation.w==0)rotation=Quaternion.identity;
                int slot=Slot(id);float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f;
                float edge=Mathf.Min(bevel,Mathf.Min(x,Mathf.Min(y,z))*.75f);
                bool stone=id=="rock"||id=="rock-light";
                float seed=center.x*13.7f+center.z*9.3f;
                var rings=new Vector3[4][];
                for(int r=0;r<4;r++)
                {
                    bool cap=r==0||r==3;float inset=cap?edge:0,cut=cap?edge*.35f:edge;
                    float xx=x-inset,zz=z-inset,yy=r==0?-y:r==1?-y+edge:r==2?y-edge:y;
                    rings[r]=new[]{P(-xx+cut,yy,-zz),P(xx-cut,yy,-zz),P(xx,yy,-zz+cut),P(xx,yy,zz-cut),P(xx-cut,yy,zz),P(-xx+cut,yy,zz),P(-xx,yy,zz-cut),P(-xx,yy,-zz+cut)};
                    for(int i=0;i<8;i++)
                    {
                        var p=rings[r][i];
                        if(stone)
                        {
                            p.x+=Mathf.Sin(seed+i*2.7f)*x*.075f;p.z+=Mathf.Cos(seed+i*3.3f)*z*.07f;
                            if(r==3)p.y+=Mathf.Sin(seed+i*1.9f)*y*.12f;
                        }
                        rings[r][i]=center+rotation*p;
                    }
                }
                for(int r=0;r<3;r++)for(int i=0;i<8;i++)
                {
                    int j=(i+1)%8;Vector3 normal=(rings[r][i]+rings[r][j]+rings[r+1][j]+rings[r+1][i])*.25f-center;
                    Quad(slot,rings[r][i],rings[r][j],rings[r+1][j],rings[r+1][i],normal);
                }
                for(int i=0;i<8;i++)
                {
                    Face(slot,center+rotation*P(0,-y,0),rings[0][i],rings[0][(i+1)%8],rotation*Vector3.down);
                    Face(slot,center+rotation*(stone?P(x*.12f,y+edge*.35f,-z*.10f):P(0,y,0)),rings[3][i],rings[3][(i+1)%8],rotation*Vector3.up);
                }
            }
            public void Cylinder(string id,Vector3 center,float radius,float length,Vector3 axis,int segments,float widthRatio=1)
            {
                int slot=Slot(id);Quaternion q=Quaternion.FromToRotation(Vector3.up,axis.normalized);
                Quaternion inverse=Quaternion.Inverse(q);
                Vector3 lo=center-axis.normalized*length*.5f,hi=center+axis.normalized*length*.5f;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,c=(i+1)*Mathf.PI*2/segments;
                    Vector3 p=P(Mathf.Cos(a)*radius*widthRatio,0,Mathf.Sin(a)*radius),s=P(Mathf.Cos(c)*radius*widthRatio,0,Mathf.Sin(c)*radius);
                    int first=vertices.Count;
                    Quad(slot,lo+q*p,lo+q*s,hi+q*s,hi+q*p,q*(p+s));
                    for(int vertex=first;vertex<vertices.Count;vertex++)
                    {
                        var point=inverse*(vertices[vertex]-center);
                        curvedNormals[vertex]=q*P(point.x/(widthRatio*widthRatio),0,point.z).normalized;
                    }
                    Face(slot,lo,lo+q*p,lo+q*s,-axis);Face(slot,hi,hi+q*p,hi+q*s,axis);
                }
            }
            public void Ring(string id,Vector3 center,float rx,float rz,float tube,Quaternion rotation)
            {
                int slot=Slot(id),first=vertices.Count;const int segments=16,sides=6;
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    float angle=i*2*Mathf.PI/segments,section=j*2*Mathf.PI/sides;
                    var point=RingPoint(angle,section,rx,rz,tube);
                    var normal=rotation*P(Mathf.Cos(angle)*Mathf.Cos(section),Mathf.Sin(section),Mathf.Sin(angle)*Mathf.Cos(section));
                    SurfaceVertex(center+rotation*point,normal);
                }
                for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
                {
                    int a=first+i*sides+j,b=first+((i+1)%segments)*sides+j,next=(j+1)%sides;
                    SurfaceQuad(slot,a,b,first+((i+1)%segments)*sides+next,first+i*sides+next,curvedNormals[a]);
                }
            }
            static Vector3 RingPoint(float a,float u,float rx,float rz,float t)=>P(Mathf.Cos(a)*(rx+Mathf.Cos(u)*t),Mathf.Sin(u)*t,Mathf.Sin(a)*(rz+Mathf.Cos(u)*t));
            public void Crystal(string id,Vector3 center,Vector3 size,Quaternion rotation)
            {
                int slot=Slot(id);Vector3[] corners={P(-.5f,0,-.5f),P(.5f,0,-.5f),P(.5f,0,.2f),P(0,.35f,.5f),P(-.5f,0,.2f)};
                Vector3 peak=center+rotation*Vector3.Scale(P(0,1,0),size);
                for(int i=0;i<corners.Length;i++)
                {
                    Vector3 a=center+rotation*Vector3.Scale(corners[i],size),c=center+rotation*Vector3.Scale(corners[(i+1)%corners.Length],size);
                    Face(slot,a,c,peak,Vector3.up);Face(slot,center,c,a,Vector3.down);
                }
            }
            public Model Finish(string name,float width,float depth,bool upright,Bounds? footprint)
            {
                // Rotation +90 about X on the art node turns -Z into height.
                for(int i=0;i<vertices.Count;i++)
                {
                    var v=vertices[i];
                    vertices[i]=upright?P(v.x/width,v.y/depth,v.z):P(v.x/width,v.z/depth,-v.y);
                }
                Vector3 fit=Vector3.one;
                if(footprint.HasValue)
                {
                    var generated=new Bounds(vertices[0],Vector3.zero);
                    foreach(var point in vertices)generated.Encapsulate(point);
                    var target=footprint.Value;var a=target.size;var b=generated.size;
                    fit=new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
                    for(int i=0;i<vertices.Count;i++)vertices[i]=target.center+Vector3.Scale(vertices[i]-generated.center,fit);
                }
                var mesh=new Mesh {name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=surfaces.Count;
                for(int i=0;i<indices.Count;i++)mesh.SetTriangles(indices[i],i);
                bool allSmooth=curvedNormals.Count==vertices.Count;
                if(!allSmooth)mesh.RecalculateNormals();
                if(curvedNormals.Count>0)
                {
                    var normals=allSmooth?new Vector3[vertices.Count]:mesh.normals;
                    foreach(var pair in curvedNormals)
                    {
                        var n=pair.Value;
                        var local=upright?P(n.x*width,n.y*depth,n.z):P(n.x*width,n.z*depth,-n.y);
                        normals[pair.Key]=P(local.x/fit.x,local.y/fit.y,local.z/fit.z).normalized;
                    }
                    mesh.normals=normals;
                }
                mesh.RecalculateBounds();
                return new Model {mesh=mesh,surfaces=surfaces.ToArray()};
            }
        }
    }
}

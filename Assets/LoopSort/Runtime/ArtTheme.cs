using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LoopSort0
{
    // All UI aliases share this skin. Atlas cells are addressed without making
    // cropped duplicate textures.
    public sealed class ThemeSpriteDefinition {public string atlas;public int index;public bool panel;}
    public static class ArtTheme
    {
        // Artist/source plates include a two-pixel white/transparent side gutter.
        // Inset by three texels so bilinear filtering cannot sample that gutter.
        internal static Rect BackgroundUv(Texture texture)
        {
            float inset=texture==null?0:3f/Mathf.Max(8,texture.width);
            return new Rect(inset,0,1-2*inset,1);
        }
        public static Color Ink=>HotUpdate.LuaUi.Color("ink",Hex(0x49263E));
        public static Color Cream=>HotUpdate.LuaUi.Color("cream",Hex(0xFFF0D0));
        public static Color Plum=>HotUpdate.LuaUi.Color("plum",Hex(0x603B55));
        public static Color Mint=>HotUpdate.LuaUi.Color("mint",Hex(0x61C7B4));
        public static Color Teal=>HotUpdate.LuaUi.Color("teal",Hex(0x286363));
        public static Color Amber=>HotUpdate.LuaUi.Color("amber",Hex(0xFFB83E));
        public static Color Caramel=>HotUpdate.LuaUi.Color("caramel",Hex(0xC88D65));
        public static Color Coral=>HotUpdate.LuaUi.Color("coral",Hex(0xFF625E));
        public static Color Hex(uint rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
        static readonly Dictionary<string,ThemeSpriteDefinition> cells=new Dictionary<string,ThemeSpriteDefinition>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static Dictionary<string,float[][]> rectangles;
        const string BatchAtlas="ui-batch";
        static Sprite whiteSprite;
        static ThemeSpriteDefinition Definition(string id)
        {
            if(id==null||!HotUpdate.LuaGameRuntime.Ready)return null;
            if(!cells.TryGetValue(id,out var definition))cells[id]=definition=(ThemeSpriteDefinition)HotUpdate.LuaGameRuntime.Call("ui.assets","sprite",id);
            return definition;
        }
        public static bool Contains(string id)=>Definition(id)!=null;
        public static bool IsPanel(string id)=>Definition(id)?.panel??false;
        internal static bool ResponsivePanel(string id)=>Definition(id)?.atlas=="approved-menu"||Definition(id)?.atlas=="menu-details";
        internal static bool StandalonePanel(string id)
        {
            var cell=Definition(id);
            return cell!=null&&cell.panel&&cell.atlas!="approved-menu"&&cell.atlas!="menu-details"&&cell.atlas!="reference-ui";
        }
        static void LoadRectangles()
        {
            if(rectangles==null)rectangles=JsonConvert.DeserializeObject<Dictionary<string,float[][]>>(
                HotUpdate.HotContent.Text("LoopSort/Theme/atlas"));
        }
        internal static float[] AtlasCell(string atlas,int index)
        {
            LoadRectangles();
            return rectangles.TryGetValue(atlas,out var cells)&&index>=0&&index<cells.Length?cells[index]:null;
        }
        internal static Rect ContentBounds(string id)
        {
            var cell=Definition(id);if(cell==null)return new Rect(0,0,1,1);
            LoadRectangles();
            if(!rectangles.TryGetValue(cell.atlas+".content",out var values)||cell.index>=values.Length)return new Rect(0,0,1,1);
            var r=values[cell.index];return new Rect(r[0],r[1],r[2],r[3]);
        }
        public static Sprite Sprite(string id)
        {
            var cell=Definition(id);if(cell==null)return null;
            if(sprites.TryGetValue(id,out var sprite))return sprite;
            // A remote release can replace individual source sheets. Keep that
            // release's artwork authoritative unless it supplies its own pack.
            var bounds=PackedCell(cell.atlas,cell.index);
            var texture=bounds!=null?Texture(BatchAtlas):null;
            if(texture==null){texture=Texture(cell.atlas);bounds=AtlasCell(cell.atlas,cell.index);}
            if(texture==null||bounds==null)return null;
            var rect=PixelRect(texture,bounds);
            // Metadata trims alpha gutters so different silhouettes are centered
            // by their actual artwork. Panel borders protect rounded corners.
            Vector4 border=Vector4.zero;
            if(cell.panel)
            {
                float[] edges=rectangles.TryGetValue(cell.atlas+".borders",out var borders)&&cell.index<borders.Length?
                    borders[cell.index]:new[]{.22f,.22f,.22f,.22f};
                border=new Vector4(rect.width*edges[0],rect.height*edges[1],rect.width*edges[2],rect.height*edges[3]);
            }
            sprite=UnityEngine.Sprite.Create(texture,rect,Vector2.one*.5f,200,0,SpriteMeshType.FullRect,border);
            sprite.name="Toy workshop / "+id;sprites[id]=sprite;return sprite;
        }
        static Texture2D Texture(string atlas)
        {
            if(!textures.TryGetValue(atlas,out var texture))
                textures[atlas]=texture=HotUpdate.HotContent.Texture("LoopSort/Theme/"+atlas);
            return texture;
        }
        static float[] PackedCell(string atlas,int index)
            =>HotUpdate.HotContent.CanUseThemeAtlas(BatchAtlas)?AtlasCell(BatchAtlas+"."+atlas,index):null;
        static Rect PixelRect(Texture2D texture,float[] bounds)
            =>new Rect(bounds[0]*texture.width,bounds[1]*texture.height,bounds[2]*texture.width,bounds[3]*texture.height);
        internal static Sprite WhiteSprite
        {
            get
            {
                if(whiteSprite!=null||!HotUpdate.LuaGameRuntime.Ready)return whiteSprite;
                var bounds=PackedCell("white",0);
                var texture=bounds!=null?Texture(BatchAtlas):null;
                if(texture!=null)
                    whiteSprite=UnityEngine.Sprite.Create(texture,PixelRect(texture,bounds),Vector2.one*.5f,200,0,SpriteMeshType.FullRect);
                return whiteSprite;
            }
        }
        public static Color CargoColor(int id,Color original)=>(Color)HotUpdate.LuaGameRuntime.Call("ui.assets","cargo_color",id,original);
        public static Material Surface(Color color,int pattern=0)
        {
            var material=new Material(HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/ToySurface"));
            material.color=color;material.SetFloat("_Pattern",pattern);material.enableInstancing=true;return material;
        }
        public static Material FeatureMaterial(int id)=>(Material)HotUpdate.LuaGameRuntime.Call("ui.assets","feature_material",id);
        internal static void ReleaseCaches()
        {
            foreach(var sprite in sprites.Values)if(sprite!=null)UnityEngine.Object.Destroy(sprite);
            if(whiteSprite!=null)UnityEngine.Object.Destroy(whiteSprite);
            whiteSprite=null;
            sprites.Clear();textures.Clear();cells.Clear();rectangles=null;
        }
    }
}

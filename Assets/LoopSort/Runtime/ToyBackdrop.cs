using UnityEngine;

namespace LoopSort0
{
    // A camera-aligned decorative layer, excluded from all level bounds/physics.
    public sealed class ToyBackdrop:MonoBehaviour
    {
        Camera view;SpriteRenderer image;Material material;
        public static ToyBackdrop Create(Camera camera)
        {
            var go=new GameObject("Warm tiled workshop background",typeof(SpriteRenderer),typeof(ToyBackdrop));
            go.transform.SetParent(camera.transform,false);go.transform.localPosition=new Vector3(0,0,95);
            var backdrop=go.GetComponent<ToyBackdrop>();backdrop.view=camera;backdrop.image=go.GetComponent<SpriteRenderer>();
            backdrop.image.sprite=GameAssets.Sprite("gameBackground");
            backdrop.material=new Material(HotUpdate.HotContent.Load<Shader>("LoopSort/Shaders/ToyBackdrop"));
            if(backdrop.image.sprite!=null)backdrop.material.mainTexture=backdrop.image.sprite.texture;
            backdrop.image.sharedMaterial=backdrop.material;backdrop.Resize();return backdrop;
        }
        public void Resize(float bannerInsetPixels=0)
        {
            if(view==null||image.sprite==null)return;
            Vector2 size=image.sprite.bounds.size;
            // Fit the full artwork to both viewport axes, including tall phones.
            float height=view.orthographicSize*2;
            transform.localScale=new Vector3(height*view.aspect/size.x,height/size.y,1);

            if(Screen.height<=0)return;
            Rect safe=GameHud.SafeArea(bannerInsetPixels);
            float screenBottom=safe.yMin/Screen.height,safeHeight=safe.height/Screen.height;
            // Match the artwork to the control row, including a narrow margin along both rails.
            const float margin=.006f;
            float beltBottom=screenBottom+safeHeight*(GameHud.ControlsBottom-margin);
            float beltTop=screenBottom+safeHeight*(GameHud.ControlsTop+margin);
            material.SetVector("_ControlBand",new Vector4(screenBottom,beltBottom,beltTop,
                Mathf.Max(.30f,beltTop+.17f)));
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}

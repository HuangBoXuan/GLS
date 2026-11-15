using UnityEngine;

namespace LoopSort0
{
    public sealed class PieceView : MonoBehaviour
    {
        public int Id; public Rigidbody Body; public float ReceiveAfter; public int LastCarrier = -1;
        public MeshRenderer Renderer;
        internal MeshFilter Filter;
        internal CargoBatchRenderer Batch;
        internal int BatchIndex=-1;
        Color sourceTint,shaderTint;
        internal Color Tint
        {
            get=>shaderTint;
            set
            {
                if(sourceTint.Equals(value))return;
                sourceTint=value;
                // SetVectorArray skips the sRGB conversion done by SetColor on
                // whole blocks. Cache the matching shader color when tint changes.
                shaderTint=QualitySettings.activeColorSpace==ColorSpace.Linear?value.linear:value;
            }
        }
        internal PhysicMaterial PhysicsMaterial;
        internal int BeltIndex=-1,CargoCarrier=-1,CargoSlot=-1;
        SphereCollider beltCollider;
        bool animated;
        public float TrackDistance, Travelled;
        public Vector3 PausedVelocity, PausedAngularVelocity;
        void OnEnable(){if(Batch!=null)Batch.Add(this);}
        void OnDisable(){if(Batch!=null)Batch.Remove(this);}
        public void SetAnimatedPose(Vector3 position,Quaternion rotation)
        {
            CargoCarrier=-1;CargoSlot=-1;
            if(!animated&&Body!=null)
            {
                Body.interpolation=RigidbodyInterpolation.None;Body.isKinematic=true;Body.detectCollisions=false;
                Body.position=position;Body.rotation=rotation;animated=true;
            }
            // Native jumps update Transform; Rigidbody state changes only at transitions.
            transform.SetPositionAndRotation(position,rotation);
        }
        internal void PreparePhysics()
        {
            if(Body!=null)return;
            beltCollider=gameObject.AddComponent<SphereCollider>();beltCollider.enabled=false;
            beltCollider.radius=.21f;beltCollider.contactOffset=.01f;beltCollider.sharedMaterial=PhysicsMaterial;
            Body=gameObject.AddComponent<Rigidbody>();Body.detectCollisions=false;Body.isKinematic=true;
            Body.mass=4;Body.drag=.1f;Body.angularDrag=10;
            Body.solverIterations=1;Body.solverVelocityIterations=1;
            Body.maxLinearVelocity=BeltSteering.MaxSpeed;Body.maxAngularVelocity=50;Body.useGravity=true;
            Body.sleepThreshold=.005f;Body.maxDepenetrationVelocity=10;
            Body.collisionDetectionMode=CollisionDetectionMode.Discrete;
            animated=true;
        }
        public void StartPhysics(Vector3 position,Quaternion rotation,Vector3 velocity)
        {
            PreparePhysics();
            animated=false;
            CargoCarrier=-1;CargoSlot=-1;
            // Clear interpolation history before activating a cube that was hidden or animated.
            Body.interpolation=RigidbodyInterpolation.None;Body.position=position;Body.rotation=rotation;
            transform.SetPositionAndRotation(position,rotation);Body.isKinematic=false;
            if(beltCollider!=null&&!beltCollider.enabled)beltCollider.enabled=true;
            Body.detectCollisions=true;
            Body.velocity=velocity;Body.interpolation=RigidbodyInterpolation.Interpolate;
        }
    }
    public sealed class CarrierClickTarget : MonoBehaviour { public int Index; }
}

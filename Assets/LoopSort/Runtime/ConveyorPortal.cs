using UnityEngine;

namespace LoopSort0
{
    // The source uses the end prefab's trigger, rather than a per-block distance test.
    public sealed class ConveyorPortal : MonoBehaviour
    {
        public Vector3 destination,forward;
        public Quaternion offsetRotation;
        void OnTriggerEnter(Collider other)
        {
            var piece=other.GetComponent<PieceView>();
            if(piece==null||piece.BeltIndex<0)return;
            var body=piece.Body;
            body.position=destination+offsetRotation*(body.position-transform.position);
            if(!body.isKinematic)body.velocity=forward*body.velocity.magnitude;
        }
    }
}

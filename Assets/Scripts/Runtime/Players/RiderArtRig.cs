using UnityEngine;

namespace Game
{
    /// <summary>Procedural articulation for the source character's rigid mesh parts (not a Humanoid skeleton).</summary>
    public sealed class RiderArtRig : MonoBehaviour
    {
        public static readonly string[] AssetNames =
            { "BlueRider", "RedRider", "GreenRider", "YellowRider", "PurpleRider", "OrangeRider" };

        public Transform head;
        public Transform armLeft;
        public Transform armRight;
        public Transform legLeft;
        public Transform legRight;

        Transform[] _joints;
        Quaternion[] _restRotations;
        Vector3[] _restPositions;
        float _walkPhase, _walkAmount;
        MeshFilter[][] _legMeshes;
        float[] _footRest;
        public float StepBob => Mathf.Abs(Mathf.Sin(_walkPhase*2)) * .025f * _walkAmount;

        void Awake() => CaptureRest();

        void CaptureRest()
        {
            if (_joints != null) return;
            _joints = new[] { head, armLeft, armRight, legLeft, legRight };
            _restRotations = new Quaternion[_joints.Length];
            _restPositions = new Vector3[_joints.Length];
            for (int i = 0; i < _joints.Length; i++)
            {
                _restRotations[i] = _joints[i].localRotation;
                _restPositions[i] = _joints[i].localPosition;
            }
            _legMeshes=new[]{legLeft.GetComponentsInChildren<MeshFilter>(),legRight.GetComponentsInChildren<MeshFilter>()};
            _footRest=new[]{FootHeight(0),FootHeight(1)};
        }

        float FootHeight(int leg)
        {
            float lowest=float.PositiveInfinity;
            foreach(var filter in _legMeshes[leg])
            {
                Bounds b=filter.sharedMesh.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    lowest=Mathf.Min(lowest,transform.InverseTransformPoint(filter.transform.TransformPoint(p)).y);
                }
            }
            return lowest;
        }

        void RotateJoint(int index, Vector3 modelAxis, float angle)
        {
            Transform joint = _joints[index];
            Vector3 localAxis = joint.parent.InverseTransformDirection(transform.TransformDirection(modelAxis));
            joint.localRotation = Quaternion.AngleAxis(angle, localAxis) * _restRotations[index];
        }

        public void PoseArms(float spread, float flail)
        {
            CaptureRest();
            RotateJoint(1, Vector3.forward, -spread - flail);
            RotateJoint(2, Vector3.forward, spread - flail);
        }

        public void PoseMovement(float speed, bool airborne, float steering, float dt)
        {
            CaptureRest();
            _walkAmount=Mathf.Lerp(_walkAmount,Mathf.InverseLerp(.08f,1.3f,speed),1-Mathf.Exp(-14f*dt));
            if(!airborne)_walkPhase=Mathf.Repeat(_walkPhase+Mathf.Min(speed,3.4f)*dt*7f,Mathf.PI*2);
            RotateJoint(0, Vector3.up, steering * 10f);
            for (int i = 0; i < 2; i++)
            {
                float phase=_walkPhase+i*Mathf.PI;
                float lift=airborne?.06f:Mathf.Max(0,Mathf.Sin(phase))*.11f*_walkAmount;
                // Back-to-front swing, then a planted return stroke. Shoes stay above the deck.
                RotateJoint(3+i,Vector3.right,airborne?24f:Mathf.Cos(phase)*27f*_walkAmount);
                Transform joint=_joints[3+i];
                joint.localPosition=_restPositions[3+i];
                Vector3 forward=joint.parent.InverseTransformVector(transform.forward);
                joint.localPosition+=forward*(-Mathf.Cos(phase)*.035f*_walkAmount);
                float correction=_footRest[i]+lift-FootHeight(i);
                joint.localPosition+=joint.parent.InverseTransformVector(transform.up)*correction;
            }
        }
    }
}

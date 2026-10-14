using UnityEngine;

namespace BardBox;

// Equipped skins contain duplicate bone names. Never rebind the Animator.
// Only rotate arm bones after animation; restore before the next evaluation.
[DefaultExecutionOrder(10000)]
internal sealed class CarryPose : MonoBehaviour
{
    private sealed class Arm
    {
        internal readonly Transform Upper,Lower,Hand,Attach;
        private readonly Quaternion[] before=new Quaternion[3],written=new Quaternion[3];
        private bool applied;
        internal Arm(Transform attach)
        {Attach=attach;Hand=attach.parent;Lower=Hand.parent;Upper=Lower.parent;}
        internal bool Valid=>Attach && Hand && Lower && Upper;
        internal void Restore()
        {
            if(!applied)return;
            RestoreBone(Upper,0);RestoreBone(Lower,1);RestoreBone(Hand,2);applied=false;
        }
        private void RestoreBone(Transform bone,int i)
        {
            // Preserve another component's later changes.
            if(bone && Quaternion.Angle(bone.localRotation,written[i])<.01f)bone.localRotation=before[i];
        }
        internal void Apply(Vector3 grip,Quaternion facing,float weight)
        {
            Restore();
            Vector3 shoulder=Upper.position,elbow=Lower.position,wrist=Hand.position;
            float a=Vector3.Distance(shoulder,elbow),b=Vector3.Distance(elbow,wrist);
            if(a<.001f || b<.001f)return;
            var handRotation=facing*Quaternion.Inverse(Attach.localRotation);
            var target=grip-handRotation*Vector3.Scale(Attach.localPosition,Hand.lossyScale);
            target=Vector3.Lerp(wrist,target,weight);
            Vector3 delta=target-shoulder;
            if(delta.sqrMagnitude<.000001f)return;
            before[0]=Upper.localRotation;before[1]=Lower.localRotation;before[2]=Hand.localRotation;
            var direction=delta.normalized;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            target=shoulder+direction*distance;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var bend=Vector3.ProjectOnPlane(facing*Vector3.forward,direction);
            if(bend.sqrMagnitude<.000001f)bend=Vector3.ProjectOnPlane(facing*Vector3.right,direction);
            var desiredElbow=shoulder+direction*along+bend.normalized*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Upper.rotation=Quaternion.FromToRotation(elbow-shoulder,desiredElbow-shoulder)*Upper.rotation;
            Lower.rotation=Quaternion.FromToRotation(Hand.position-Lower.position,target-Lower.position)*Lower.rotation;
            Hand.rotation=Quaternion.Slerp(Hand.rotation,handRotation,weight);
            written[0]=Upper.localRotation;written[1]=Lower.localRotation;written[2]=Hand.localRotation;applied=true;
        }
    }

    private Musician musician=null!;
    private Arm? left,right;
    private float weight;
    private bool warned;
    private void Awake()=>musician=GetComponent<Musician>();
    private void OnEnable()=>Camera.onPreRender+=BeforeCamera;
    private void BeforeCamera(Camera camera)
    {
        // Fixed-mode Animator interpolation can overwrite LateUpdate transforms at render time.
        // Interpolation occurs between pre-cull and pre-render. Reapply only our six rotations
        // before drawing; never change the camera, root transform, Animator or skeleton layout.
        if(weight>0 && musician && musician.enabled && musician.Player && !musician.Player.IsDead() && !musician.Player.IsTeleporting())ApplyPose();
    }
    private void Update()=>Restore();
    private void LateUpdate()
    {
        if(!musician || !musician.enabled || !musician.Player){ResetPose();return;}
        var player=musician.Player;
        if(player.IsDead() || player.IsTeleporting()){ResetPose();return;}
        weight=Mathf.MoveTowards(weight,musician.ActiveTape!=null?1:0,Time.deltaTime*6);
        if(weight<=0)return;
        ApplyPose();
    }
    private void ApplyPose()
    {
        var player=musician.Player;
        var vis=player.GetVisEquipment();
        if(!vis || !ValidHand(vis.m_leftHand,"LeftHand") || !ValidHand(vis.m_rightHand,"RightHand"))
        {
            ResetPose();
            if(!warned){Plugin.Instance.Warn("Cannot resolve player arm bones; carry pose disabled until the rig is available.");warned=true;}
            return;
        }
        if(left==null || !left.Valid || left.Attach!=vis.m_leftHand || right==null || !right.Valid || right.Attach!=vis.m_rightHand)
        {Restore();left=new Arm(vis.m_leftHand);right=new Arm(vis.m_rightHand);}
        var visual=player.GetVisual().transform;
        var forward=Vector3.ProjectOnPlane(visual.forward,Vector3.up);
        if(forward.sqrMagnitude<.000001f){ResetPose();return;}
        var facing=Quaternion.LookRotation(forward.normalized,Vector3.up);
        var origin=player.transform.position;
        left.Apply(origin+facing*new Vector3(-.25f,2.02f,.08f),facing,weight);
        right.Apply(origin+facing*new Vector3(.25f,2.02f,.08f),facing,weight);
    }
    private static bool ValidHand(Transform attach,string name)=>attach && attach.parent && attach.parent.name==name && attach.parent.parent && attach.parent.parent.parent;
    private void Restore(){left?.Restore();right?.Restore();}
    private void ResetPose(){Restore();weight=0;}
    private void OnDisable(){Camera.onPreRender-=BeforeCamera;ResetPose();}
    private void OnDestroy()=>ResetPose();
}

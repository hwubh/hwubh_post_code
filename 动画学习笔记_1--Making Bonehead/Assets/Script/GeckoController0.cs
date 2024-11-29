using BoneHead;
using System.Collections;
using UnityEngine;

public class GeckoController : MonoBehaviour
{
    // 被追踪的目标
    [SerializeField] Transform target;
    // 守宫颈部骨骼
    [SerializeField] Transform headBone;
    //左右眼骨骼位置
    [SerializeField] Transform leftEyeBone;
    [SerializeField] Transform rightEyeBone;
    //左右眼的运动速度和各自的角度限制
    [SerializeField] float eyeTrackingSpeed;
    [SerializeField] float leftEyeMaxYRotation;
    [SerializeField] float leftEyeMinYRotation;
    [SerializeField] float rightEyeMaxYRotation;
    [SerializeField] float rightEyeMinYRotation;

    // 头部最大旋转角度
    [SerializeField] float headMaxTurnAngle = 45f;
    // 头部运动速度
    [SerializeField] float headTrackingSpeed = 1.0f;

    [SerializeField] LegStepper LegStepper;

    // How fast we can turn and move full throttle
    [SerializeField] float turnSpeed;
    [SerializeField] float moveSpeed;
    // How fast we will reach the above speeds
    [SerializeField] float turnAcceleration;
    [SerializeField] float moveAcceleration;
    // Try to stay in this range from the target
    [SerializeField] float minDistToTarget;
    [SerializeField] float maxDistToTarget;
    // If we are above this angle from the target, start turning
    [SerializeField] float maxAngToTarget;

    // World space velocity
    Vector3 currentVelocity;
    // We are only doing a rotation around the up axis, so we only use a float here
    float currentAngularVelocity;

    public float speed = 0.5f;

    private void Awake()
    {
        LegUpdateCoroutine();
    }

    // 调用 LateUpdate 来更新我们所有的动画逻辑
    // 其次序在游戏逻辑(Update())与渲染逻辑之间
    // 前者保证动画使用正确的数据
    // 后者保证动画与渲染结果相符合
    // 关于Unity事件函数的执行循序：https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html
    void LateUpdate()
    {
        // 从靠近根节点的骨骼开始更新
        RootMotionUpdate();
        HeadTrackingUpdate();
        EyeTrackingUpdate();
    }

    void HeadTrackingUpdate()
    {
        // 世界坐标向量：从头部指向目标的向量。
        Vector3 towardObjectFromHead = target.position - headBone.position;
        // 记录headbone当前的局部旋转
        Quaternion currentLcoalRotation = headBone.localRotation;
        // 当headBone的局部旋转被置空后，headbone 和 headboned的父节点相对于世界空间的变换相同。
        headBone.localRotation = Quaternion.identity;
        var targetLocalLookDir = headBone.InverseTransformDirection(towardObjectFromHead);
        // 相当于一个Clamp操作，将角度限制在0到headMaxTurnAngle之间。
        targetLocalLookDir = Vector3.RotateTowards(Vector3.forward, targetLocalLookDir, Mathf.Deg2Rad * headMaxTurnAngle, 0);
        // 计算目标旋转在局部空间下的表达。
        Quaternion targetLocalRotation = Quaternion.LookRotation(targetLocalLookDir, Vector3.up);
        // 针对局部旋转进行Slerp插值。
        headBone.localRotation = Quaternion.Slerp(
            currentLcoalRotation, targetLocalRotation,
            1 - Mathf.Exp(-headTrackingSpeed * Time.deltaTime));

        //调试代码
        //{
        //    Debug.DrawLine(headBone.position, headBone.position + headBone.forward * 10, Color.red);
        //    //显示头部的旋转范围
        //    var length = Mathf.Tan(headMaxTurnAngle * Mathf.Deg2Rad) * 3;
        //    var jointPosPP = headBone.position + headBone.parent.TransformDirection(new Vector3(length, length, 3));
        //    var jointPosNP = headBone.position + headBone.parent.TransformDirection(new Vector3(-length, length, 3));
        //    var jointPosPN = headBone.position + headBone.parent.TransformDirection(new Vector3(length, -length, 3));
        //    var jointPosNN = headBone.position + headBone.parent.TransformDirection(new Vector3(-length, -length, 3));
        //    Debug.DrawLine(headBone.position, jointPosPP, Color.blue);
        //    Debug.DrawLine(headBone.position, jointPosNP, Color.blue);
        //    Debug.DrawLine(headBone.position, jointPosPN, Color.blue);
        //    Debug.DrawLine(headBone.position, jointPosNN, Color.blue);
        //    Debug.DrawLine(jointPosPP, jointPosNP, Color.blue);
        //    Debug.DrawLine(jointPosNP, jointPosNN, Color.blue);
        //    Debug.DrawLine(jointPosNN, jointPosPN, Color.blue);
        //    Debug.DrawLine(jointPosPN, jointPosPP, Color.blue);
        //}
    }

    void EyeTrackingUpdate()
    {
        // 计算目标旋转在世界空间下的表达。
        // 左右眼均指向“headbone”指向 target的方向。
        // 左右眼的指向平行，避免斗鸡眼的情况出现。
        Quaternion targetEyeRotation = Quaternion.LookRotation(target.position - headBone.position, transform.up);

        // 更新左眼的世界空间旋转
        leftEyeBone.rotation = Quaternion.Slerp(leftEyeBone.rotation, targetEyeRotation, 
            1 - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime));
        // 更新右眼的世界空间旋转
        rightEyeBone.rotation = Quaternion.Slerp(rightEyeBone.rotation, targetEyeRotation,
            1 - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime));

        // 得到左右眼再局部空间下的绕Y轴的旋转
        float leftEyeCurrentYRotation = leftEyeBone.localEulerAngles.y;
        float rightEyeCurrentYRotation = rightEyeBone.localEulerAngles.y;

        // 映射范围外的角度到-180°~180°之间
        if (leftEyeCurrentYRotation > 180)
        {
            leftEyeCurrentYRotation -= 360;
        }
        if (rightEyeCurrentYRotation > 180)
        {
            rightEyeCurrentYRotation -= 360;
        }

        // 限制左右眼在局部空间中绕Y轴的旋转
        float leftEyeClampedYRotation = Mathf.Clamp(leftEyeCurrentYRotation, leftEyeMinYRotation, leftEyeMaxYRotation);
        float rightEyeClampedYRotation = Mathf.Clamp(rightEyeCurrentYRotation,rightEyeMinYRotation,rightEyeMaxYRotation);

        //更新左右眼在局部空间中绕Y轴的旋转
        leftEyeBone.localEulerAngles = new Vector3(leftEyeBone.localEulerAngles.x, leftEyeClampedYRotation, leftEyeBone.localEulerAngles.z);
        rightEyeBone.localEulerAngles = new Vector3(rightEyeBone.localEulerAngles.x, rightEyeClampedYRotation, rightEyeBone.localEulerAngles.z);
    }

    void LegUpdateCoroutine()
    {
        StartCoroutine(LegStepper.TryMove());
    }

    void RootMotionUpdate() 
    {
        Vector3 towardTarget = target.position - transform.position;
        Vector3 towardTargetProjected = Vector3.ProjectOnPlane(towardTarget, transform.up);
        float angToTarget = Vector3.SignedAngle(transform.forward, towardTargetProjected, transform.up);

        float targetAngularVelocity = 0;

        if(Mathf.Abs(angToTarget) > maxAngToTarget)
        {
            if(angToTarget > 0) 
            {
                targetAngularVelocity = turnSpeed;
            }
            else 
            {
                targetAngularVelocity = -turnSpeed;
            }
        }

        currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, targetAngularVelocity, 1 - Mathf.Exp(-turnAcceleration * Time.deltaTime));

        transform.Rotate(0, Time.deltaTime * currentAngularVelocity, 0, Space.World);

        Vector3 targetVelocity = Vector3.zero;

        if(Mathf.Abs(angToTarget) < 90) 
        {
            float distToTarget = Vector3.Distance(transform.position, target.position);

            if(distToTarget > maxDistToTarget) 
            {
                targetVelocity = moveSpeed * towardTargetProjected.normalized;
            }
            else if(distToTarget < minDistToTarget) 
            {
                targetVelocity = moveSpeed * -towardTargetProjected.normalized;
            }
        }

        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1 - Mathf.Exp(-moveAcceleration * Time.deltaTime));

        transform.position += currentVelocity * Time.deltaTime;
    }
}

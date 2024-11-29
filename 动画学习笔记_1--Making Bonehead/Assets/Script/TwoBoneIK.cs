using System.Collections.Generic;
using UnityEngine;


public class TwoBoneIK : MonoBehaviour
{
    public List<Transform> thighTransforms;
    public List<Transform> poleTransforms;
    public List<Transform> effectorTransforms;
    private List<Transform> kneeTransforms = new List<Transform>();
    private List<Transform> ankleTransforms = new List<Transform>();

    private Quaternion verticalQuaternion0 = Quaternion.Euler(-90, 180, 90);
    private Quaternion verticalQuaternion1 = Quaternion.Euler(90, 0, -90);

    private void Start()
    {
        for(int i = 0; i < thighTransforms.Count; i++) 
        {
            kneeTransforms.Add(thighTransforms[i].GetChild(0));
            ankleTransforms.Add(kneeTransforms[i].GetChild(0));
        }

    }

    private void Update()
    {
        for (int i = 0; i < thighTransforms.Count; i++)
        {
            IKSolver(thighTransforms[i], kneeTransforms[i], ankleTransforms[i], poleTransforms[i], effectorTransforms[i]);
        }
    }

    public void IKSolver(Transform thighTransform, Transform kneeTransform, Transform ankleTransform, Transform poleTransform, Transform effectorTransform)
    {
        // 世界空间向量：从肩到Pole
        var t2pTranslation = poleTransform.position - thighTransform.position;
        // 世界空间向量：从肩到Effector
        var t2eTranslation = effectorTransform.position - thighTransform.position;
        //将肩的局部坐标的正Z方向指向effector, 正y方向与指向pole的位置的方向相似。换言之，肩，pole，effector共面。
        thighTransform.rotation = Quaternion.LookRotation(t2eTranslation, t2pTranslation);

        // 世界空间向量：从肩到肘
        var t2mTranslation = kneeTransform.position - thighTransform.position;
        // 世界空间向量：从肩到肘所绕行的旋转轴
        var normal = Vector3.Cross(t2mTranslation, t2eTranslation).normalized;
        // 如果二者的叉乘为0，则二者同向或相向。
        // 因此二者在同一平面上，使用该平面的法线作为旋转轴
        if (Mathf.Approximately(normal.magnitude, 0f))
        {
            normal = Vector3.Cross(t2pTranslation, t2eTranslation).normalized;
        }

        // 移除一些因为float精度导致的计算误差。
        if (Mathf.Abs(normal.x) < 0.01f)
            normal.x = 0f;
        if (Mathf.Abs(normal.y) < 0.01f)
            normal.y = 0f;
        if (Mathf.Abs(normal.z) < 0.01f)
            normal.z = 0f;

        // 调试代码，画出effector与肩的连线
        // 画出旋转轴
        Debug.DrawRay(thighTransform.position, t2eTranslation, Color.red);
        Debug.DrawRay(thighTransform.position, normal * 10, Color.black);

        // 计算肩到肘，与肩到effector所构成的夹角
        var thighRotationAngle = Vector3.SignedAngle(t2mTranslation.normalized, t2eTranslation.normalized, normal);
        // 忽略精度导致的计算误差
        if ((t2mTranslation - t2eTranslation).magnitude < 0.01f || Mathf.Abs(thighRotationAngle) < Mathf.PI / 180f)
            thighRotationAngle = 0f;
        // 将旋转轴从世界空间转换到肩的局部空间中
        normal = thighTransform.InverseTransformDirection(normal);
        // 计算旋转的四元数表达
        var thighRotation = Quaternion.AngleAxis(thighRotationAngle, normal);
        // 让肩，肘，effector共线。
        thighTransform.localRotation *= thighRotation;

        // 世界空间向量：从肘到腕
        var k2tTranslation = ankleTransform.position - kneeTransform.position;
        // 肩到肘的线段长度
        var t2mLength = t2mTranslation.magnitude;
        // 肘到腕的线段长度
        var k2tLength = k2tTranslation.magnitude;
        // 肩到effector的线段长度需要介于另两边的和与差之间
        var eps = 0.00001f;
        var t2eLength = Mathf.Clamp(t2eTranslation.magnitude, Mathf.Abs(t2mLength - k2tLength) + eps, t2mLength + k2tLength - eps);

        // 计算腕->肩->肘构成的角度，即肩部要旋转的角度
        var a2t2mAngle = Mathf.Acos(Mathf.Clamp((t2mLength * t2mLength + t2eLength * t2eLength - k2tLength * k2tLength)
                                        / (2 * t2mLength * t2eLength), -1, 1));
        // 计算肩->肘->腕构成的角度的补角，即肘部要旋转的角度
        var t2k2tAngle = Mathf.PI - Mathf.Acos(Mathf.Clamp((t2mLength * t2mLength + k2tLength * k2tLength - t2eLength * t2eLength)
                                / (2 * t2mLength * k2tLength), -1, 1));

        // 规定从pole旋转到effector的方向为角度的正方向
        normal = Vector3.Cross(t2pTranslation, t2eTranslation).normalized;
        // 如果二者的叉乘为0，则二者同向或相向。
        // 因此二者在同一平面上，使用该肩到腕，转到肩到effector的方向为正方向。
        if (Mathf.Approximately(normal.magnitude, 0f))
        {
            var t2tTranslation = ankleTransform.position - thighTransform.position;
            normal = Vector3.Cross(t2tTranslation, t2eTranslation).normalized;
        }
        // 移除一些因为float精度导致的计算误差。
        if (Mathf.Abs(normal.x) < 0.01f)
            normal.x = 0f;
        if (Mathf.Abs(normal.y) < 0.01f)
            normal.y = 0f;
        if (Mathf.Abs(normal.z) < 0.01f)
            normal.z = 0f;

        // 调试代码，画出旋转轴
        Debug.DrawRay(thighTransform.position, normal * 10, Color.yellow);

        // 忽略精度导致的计算误差
        if (Mathf.Abs(a2t2mAngle) < Mathf.PI / 180f)
            a2t2mAngle = 0;
        if (Mathf.Abs(t2k2tAngle) < Mathf.PI / 180f)
            t2k2tAngle = 0;

        // 肩到肘的向量绕着旋转轴旋转
        // 因为我们希望肘与pole在同一侧，所以这里顺时针旋转(旋转轴的逆方向: -normal)
        var t2mRotation = Quaternion.AngleAxis(a2t2mAngle * Mathf.Rad2Deg, thighTransform.InverseTransformDirection(-normal));
        // 将肘的局部旋转置零，即肘到腕的向量与肩到肘的向量同向。
        thighTransform.rotation = kneeTransform.rotation = thighTransform.rotation * t2mRotation;
        // 肘到腕的向量绕着旋转轴旋转
        // 逆着肩到肘的向量的旋转方向进行旋转
        var k2tRotation = Quaternion.AngleAxis(t2k2tAngle * Mathf.Rad2Deg, kneeTransform.InverseTransformDirection(normal));
        kneeTransform.rotation = thighTransform.rotation * k2tRotation;

        //调整腕部的旋转使之与effector期望的旋转相同
        ankleTransform.rotation = effectorTransform.rotation;
    }
}

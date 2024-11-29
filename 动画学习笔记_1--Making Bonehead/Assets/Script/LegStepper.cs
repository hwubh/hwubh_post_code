using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BoneHead 
{
    public class LegStepper : MonoBehaviour
    {
        // “原点”和effector的Transforms。
        public List<Transform> homeTransforms;
        public List<Transform> effectorTransfroms;
        // 以“原点”为中心圈定的范围的半径
        // 即肢体可以离开“原点”的最大距离
        [SerializeField] float wantStepAtDistance;
        // 每一步需要花费多长时间完成
        [SerializeField] float moveDuration;

        [SerializeField] float stepOvershootFraction;

        // 是否正在移动
        private List<bool> Movings = new List<bool>();

        public IEnumerator TryMove()
        {
            for (int i = 0; i < homeTransforms.Count; i++)
                Movings.Add(false);

            while (true)
            {
                do
                {
                    move(0);
                    move(3);
                    yield return null;

                } while (Movings[0] || Movings[3]);

                do
                {
                    move(1);
                    move(2);
                    yield return null;
                } while (Movings[1] || Movings[2]);
            }
        }
        
        private void move(int i)
        {
            // 如果正在移动，不另外开启协程
            if (Movings[i])
                return;

            if (effectorTransfroms?[i] == null)
                return;

            float distFromHome = Vector3.Distance(effectorTransfroms[i].position, homeTransforms[i].position);

            // 如果肢体超过了范围
            if (distFromHome > wantStepAtDistance)
            {
                // 开始移动（创建协程）
                StartCoroutine(MoveToHome(i));
            }
        }

        IEnumerator MoveToHome(int i)
        {
            Transform effectorTransfrom = effectorTransfroms[i];
            Transform homeTransform = homeTransforms[i];

            // 表明正在移动中
            // 肢体正在执行一个协程，避免重复创建与冲突
            Movings[i] = true;

            // 保存初始数据
            Quaternion startRot = effectorTransfrom.rotation;
            Vector3 startPos = effectorTransfrom.position;

            Quaternion endRot = homeTransform.rotation;

            // 计算过冲向量在XZ方向的偏移
            Vector3 towardHome = (homeTransform.position - effectorTransfrom.position);
            float overshootDistance = wantStepAtDistance * stepOvershootFraction;
            Vector3 overshootVector = towardHome * overshootDistance;
            overshootVector = Vector3.ProjectOnPlane(overshootVector, Vector3.up);

            // 在原点位置位置上加上过冲带来的偏移
            Vector3 endPos = homeTransform.position + overshootVector;

            // 计算运动轨迹的中点
            // 在Y方向上加上一些偏移，使步伐挑起移动距离的一半
            Vector3 centrePos = (startPos + endPos) / 2;
            centrePos += Vector3.up * Vector3.Distance(startPos, endPos) / 2f;

            // 步伐开始的时间
            float timeElapsed = 0;
            // 这里使用了do-loop结构，normalizedTime在最后一次循环时会超过1。原文中担心这会导致错误的结果，
            // 但Unity提供的Vector3.Lerp，Quaternion.Slerp会对传入的normalizedTime做clamp01操作，所以不用做额外的操作。
            do
            {
                timeElapsed += Time.deltaTime;

                float normalizedTime = timeElapsed / moveDuration;
                normalizedTime = Easing.Cubic.InOut(normalizedTime);

                // 二次贝塞尔曲线
                effectorTransfrom.position = Vector3.Lerp
                    ( Vector3.Lerp(startPos, centrePos, normalizedTime),
                      Vector3.Lerp(centrePos, endPos, normalizedTime),
                      normalizedTime
                    );
                effectorTransfrom.rotation = Quaternion.Slerp(startRot, endRot, normalizedTime);

                // 等待到下一帧继续执行
                yield return null;
            }
            while (timeElapsed < moveDuration);

            // 结束移动
            Movings[i] = false;
        }

		public class Easing
		{
			public class Cubic
			{
				public static float In(float k)
				{
					return k * k * k;
				}

				public static float Out(float k)
				{
					return 1f + ((k -= 1f) * k * k);
				}

				public static float InOut(float k)
				{
					if ((k *= 2f) < 1f) return 0.5f * k * k * k;
					return 0.5f * ((k -= 2f) * k * k + 2f);
				}
			};

		}
	}
}


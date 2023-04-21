using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Memo : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var start = new Vector3(1, 0, 0).normalized;
        var goal = new Vector3(2, 1, 0).normalized;
        var target = new Vector3(-1, 0, 0);

        var axis = Vector3.Cross(start, goal).normalized;
        // ベクトルaとベクトルbの内積を求める
        float angle = Mathf.Acos(Vector3.Dot(start, goal));

        // 回転行列を作成する
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        float t = 1 - c;

        Matrix4x4 rotationMatrix = Matrix4x4.identity;
        rotationMatrix.m00 = t * axis.x * axis.x + c;
        rotationMatrix.m01 = t * axis.x * axis.y - s * axis.z;
        rotationMatrix.m02 = t * axis.x * axis.z + s * axis.y;
        rotationMatrix.m10 = t * axis.x * axis.y + s * axis.z;
        rotationMatrix.m11 = t * axis.y * axis.y + c;
        rotationMatrix.m12 = t * axis.y * axis.z - s * axis.x;
        rotationMatrix.m20 = t * axis.x * axis.z - s * axis.y;
        rotationMatrix.m21 = t * axis.y * axis.z + s * axis.x;
        rotationMatrix.m22 = t * axis.z * axis.z + c;

        // ベクトルvを回転する
        var result = rotationMatrix.MultiplyPoint(target);

        Debug.DrawLine(Vector3.zero, start, Color.red);
        Debug.DrawLine(Vector3.zero, goal, Color.blue);
        Debug.DrawLine(Vector3.zero, target, Color.yellow);
        Debug.DrawLine(Vector3.zero, result, Color.green);

        Debug.Log(Vector3.Dot(start, goal) + " / " + Vector3.Dot(target, result));
    }
}

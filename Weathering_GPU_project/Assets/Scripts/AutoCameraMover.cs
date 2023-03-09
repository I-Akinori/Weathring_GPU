using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoCameraMover : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.rotation = Quaternion.AngleAxis(20f * Mathf.Sin(Time.time), Vector3.up);
    }
}

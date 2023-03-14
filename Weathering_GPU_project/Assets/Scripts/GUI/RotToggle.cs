using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RotToggle : MonoBehaviour
{
    public static bool _rot = true;
    public Toggle rottog;

    public void ChangeRot()
    {
        _rot = !_rot;
        Debug.Log(_rot);
        //vistog.isOn = _vis;
    }
}

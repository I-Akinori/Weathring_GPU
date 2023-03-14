using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VisToggle : MonoBehaviour
{
    public static bool _vis = false;
    public Toggle vistog;

    public void ChangeVis()
    {
        _vis = !_vis;
        Debug.Log(_vis);
        //vistog.isOn = _vis;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaToggle : MonoBehaviour
{
    public static bool _wea = true;
    public Toggle weatog;

    public void ChangeWea()
    {
        _wea = !_wea;
        Debug.Log(_wea);
        //vistog.isOn = _vis;
    }
}

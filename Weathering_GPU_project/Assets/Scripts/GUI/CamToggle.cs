using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Weathering_GPU
{
    public class CamToggle : MonoBehaviour
    {
        public static bool _cam = true;
        public Toggle camtog;

        public void ChangeCam()
        {
            _cam = !_cam;
            Debug.Log(_cam);
            //vistog.isOn = _vis;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Weathering_GPU
{
    public class ReverseToggle : MonoBehaviour
    {
        public static bool _reverse = false;
        //public Toggle revtog;

        public void ChangeReverse()
        {
            _reverse = !_reverse;
        }
    }
}
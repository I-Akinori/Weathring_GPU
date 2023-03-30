using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Weathering_GPU
{
    public class ControlMode : MonoBehaviour
    {
        public Dropdown dropdown;

        static public int ctrl_mode = 0;

        public void ChangeMode()
        {
            if (dropdown.value == 0)
            {
                ctrl_mode = 0;
            }
            else if (dropdown.value == 1)
            {
                ctrl_mode = 1;
            }
            else if (dropdown.value == 2)
            {
                ctrl_mode = 2;
            }
            else if (dropdown.value == 3)
            {
                ctrl_mode = 3;
            }
        }
    }
}
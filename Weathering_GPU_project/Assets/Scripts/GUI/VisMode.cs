using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Weathering_GPU
{
    public class VisMode : MonoBehaviour
    {
        public Dropdown dropdown;

        static public int vis_mode = 0;

        public void ChangeMode()
        {
            if (dropdown.value == 0)
            {
                vis_mode = 0;
            }
            else if (dropdown.value == 1)
            {
                vis_mode = 1;
            }
            else if (dropdown.value == 2)
            {
                vis_mode = 2;
            }
            else if (dropdown.value == 3)
            {
                vis_mode = 3;
            }
        }
    }
}
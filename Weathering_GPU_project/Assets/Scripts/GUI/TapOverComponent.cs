using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Weathering_GPU
{
    [RequireComponent(typeof(Image))]
    public class TapOverComponent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static public bool OnGUI = false;
        public Image image { get { return GetComponent<Image>(); } }

        // オブジェクトの範囲内にマウスポインタが入った際に呼び出されます。
        // this method called by mouse-pointer enter the object.
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnGUI = true;
        }

        // オブジェクトの範囲内からマウスポインタが出た際に呼び出されます。
        // 
        public void OnPointerExit(PointerEventData eventData)
        {
            OnGUI = false;
        }
    }
}
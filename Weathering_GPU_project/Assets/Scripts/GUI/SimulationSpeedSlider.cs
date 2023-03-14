using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Weathering_GPU
{
    public class SimulationSpeedSlider : MonoBehaviour
    {
        static public float SliderValue = 5.0f;

        [SerializeField] Slider slider;

        void Start()
        {
            Slider slider = this.GetComponent<Slider>();
            slider.onValueChanged.AddListener(OnSlide);
        }

        void OnSlide(float value)
        {
            SliderValue = value;
            TapOverComponent.OnGUI = true;
        }
    }
}
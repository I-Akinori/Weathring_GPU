using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DrawingRangeSlider : MonoBehaviour
{
    static public float DrawingRange = 0.5f;

    [SerializeField] Slider slider;

    void Start()
    {
        Slider slider = this.GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnSlide);
    }

    private void Update()
    {
        
    }
    void OnSlide(float value)
    {
        DrawingRange = value;
    }
}

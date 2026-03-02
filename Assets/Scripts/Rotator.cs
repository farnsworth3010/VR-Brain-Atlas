using UnityEngine;
using UnityEngine.UI;

public class Rotator : MonoBehaviour
{
    public Slider slider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slider.onValueChanged.AddListener((v) =>
        {
            transform.rotation = Quaternion.Euler(0, v * 360f, 0);
        }
        );
    }

    // Update is called once per frame
    void Update()
    {

    }
}

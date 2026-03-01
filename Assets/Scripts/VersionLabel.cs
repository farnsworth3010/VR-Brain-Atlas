using TMPro;
using UnityEngine;

public class VersionLabel : MonoBehaviour
{
    private TMP_Text label;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        label = GetComponent<TMP_Text>();
        label.text = "Version " + Application.version;
    }

    // Update is called once per frame
    void Update()
    {

    }
}

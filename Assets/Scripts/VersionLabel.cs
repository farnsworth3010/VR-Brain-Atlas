using TMPro;
using UnityEngine;

public class VersionLabel : MonoBehaviour
{
    private TMP_Text label;

    void Start()
    {
        label = GetComponent<TMP_Text>();
        label.text = "Version " + Application.version;
    }
}

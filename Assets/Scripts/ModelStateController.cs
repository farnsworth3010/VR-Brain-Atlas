using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ModelStateController : MonoBehaviour
{
    public GameObject model;
    [SerializeField] private Transform container;

    private bool isMovementLocked = true;
    private Rigidbody modelRigidbody;
    private XRGrabInteractable modelGrabInteractable;

    public void Start()
    {
        if (model == null)
        {
            model = gameObject;
        }

        modelRigidbody = model.GetComponent<Rigidbody>();
        modelGrabInteractable = model.GetComponent<XRGrabInteractable>();

        ApplyMovementLockState();
    }

    public void ToggleMovementLock()
    {
        isMovementLocked = !isMovementLocked;
        ApplyMovementLockState();
    }

    private void ApplyMovementLockState()
    {
        if (modelRigidbody != null)
        {
            if (isMovementLocked)
            {
                modelRigidbody.constraints |= RigidbodyConstraints.FreezePosition;
            }
            else
            {
                modelRigidbody.constraints &= ~RigidbodyConstraints.FreezePosition;
            }
        }

        if (modelGrabInteractable != null)
        {
            modelGrabInteractable.trackPosition = !isMovementLocked;
        }
    }

    public void ToggleFirstChildByName(string childName)
    {
        Transform targetContainer = container != null ? container : transform;

        Transform[] descendants = targetContainer.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in descendants)
        {
            if (child == targetContainer)
            {
                continue;
            }

            if (child.name == childName)
            {
                child.gameObject.SetActive(!child.gameObject.activeSelf);
                return;
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// 3階建てプロトタイプの階移動用エレベーターパネル。
/// </summary>
public class ElevatorPanel : MonoBehaviour, IInteractable
{
    [SerializeField] private int destinationFloor = 1;
    [SerializeField] private Vector3 destinationPosition;

    public void Configure(int floor, Vector3 position)
    {
        destinationFloor = floor;
        destinationPosition = position;
    }

    public void Interact(SpyController spy)
    {
        if (spy == null)
            return;
        spy.TeleportTo(destinationPosition);
        Debug.Log($"🛗 Elevator moved spy to floor {destinationFloor}");
    }

    public string GetInteractionPrompt()
    {
        return $"[E] エレベーターで {destinationFloor}階へ移動";
    }
}

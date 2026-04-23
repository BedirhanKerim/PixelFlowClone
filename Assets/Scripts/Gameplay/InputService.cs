using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class InputService : MonoBehaviour
{
    [Inject] private GameFlowController _flow;

    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _interactableMask = ~0;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (_flow.CurrentPhase != Phase.Playing) return;
        if (!TryGetPressedScreenPos(out var screenPos)) return;

        var ray = _camera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 500f, _interactableMask)) return;

        var interactable = hit.collider.GetComponentInParent<IInteractable>();
        interactable?.OnTap();
    }

    private static bool TryGetPressedScreenPos(out Vector2 screenPos)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }
        screenPos = default;
        return false;
    }
}

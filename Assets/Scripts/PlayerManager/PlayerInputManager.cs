using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour, PlayerInputs.IDefaultActions
{
    private PlayerInputs inputs;

    public RhythmGameDirector director;

    // Events for clean communication
    public System.Action<int> OnMoveInput; // -1 for left, +1 for right
    public int PressedColor { get; private set; }

    void Awake()
    {
        inputs = new PlayerInputs();
        inputs.Default.AddCallbacks(this);
    }

    void OnEnable()
    {
        inputs.Default.Enable();
    }

    void OnDisable()
    {
        inputs.Default.Disable();
    }

    // -------------------------------
    //   INPUT CALLBACKS
    // -------------------------------
    public void OnMovement(InputAction.CallbackContext context)
    {
        // ONLY trigger on performed (button press), not started/canceled
        if (!context.performed)
            return;

        float value = context.ReadValue<float>();

        // Convert to discrete direction
        if (Mathf.Abs(value) > 0.1f) // Deadzone
        {
            int direction = value > 0 ? 1 : -1;
            OnMoveInput?.Invoke(direction);
            Debug.Log($"Move Input: {direction}");
        }
    }

    public void OnBlue(InputAction.CallbackContext context)
    {
        Debug.Log("Pressed BLUE");
        SetColor(context, 1);
    }

    public void OnRed(InputAction.CallbackContext context)
    {
        Debug.Log("Pressed RED");
        SetColor(context, 2);
    }

    public void OnGreen(InputAction.CallbackContext context)
    {
        Debug.Log("Pressed GREEN");
        SetColor(context, 3);
    }

    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.canceled)
            return;
        director.StartGameFromMenu();
        director.RestartScene();
    }

    private void SetColor(InputAction.CallbackContext context, int colorNumber)
    {
        if (context.performed)
        {
            PressedColor = colorNumber;
        }

        PressedColor = 0;
    }
}
using UnityEngine;

public class CarInputReader : MonoBehaviour
{
    private CarControls controls;

    [Header("Debug")]
    [SerializeField] private bool logInputs = false;

    public float Steer { get; private set; }
    public float Throttle { get; private set; }
    public float Brake { get; private set; }

    public bool BoostHeld { get; private set; }
    public bool HandbrakeHeld { get; private set; }
    public bool ResetPressedThisFrame { get; private set; }
    public bool PausePressedThisFrame { get; private set; }

    private void Awake()
    {
        controls = new CarControls();
    }

    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new CarControls();
        }
        controls.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Disable();
        }
    }

    private void Update()
    {
        if (controls == null)
        {
            controls = new CarControls();
            controls.Enable();
        }

        Steer = controls.Driving.Steer.ReadValue<float>();
        Throttle = controls.Driving.Throttle.ReadValue<float>();
        Brake = controls.Driving.Brake.ReadValue<float>();

        BoostHeld = controls.Driving.Boost.IsPressed();
        HandbrakeHeld = controls.Driving.Handbrake.IsPressed();

        ResetPressedThisFrame = controls.Driving.Reset.WasPressedThisFrame();
        PausePressedThisFrame = controls.Driving.Pause.WasPressedThisFrame();

        if (logInputs && (Mathf.Abs(Steer) > 0.01f || Throttle > 0.01f || Brake > 0.01f))
        {
            Debug.Log($"INPUT - Steer: {Steer:F2}, Throttle: {Throttle:F2}, Brake: {Brake:F2}");
        }
    }
}
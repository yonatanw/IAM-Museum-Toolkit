using UnityEngine;

/// <summary>
/// Simple free camera for testing in Play mode.
/// - Right-click + drag: look around
/// - WASD / Arrow keys: move
/// - Q/E: down/up
/// - Scroll wheel: speed boost
/// - Shift: move faster
/// </summary>
public class FreeCam : MonoBehaviour
{
    [Header("Look")]
    public float lookSpeed     = 2.0f;

    [Header("Move")]
    public float moveSpeed     = 5.0f;
    public float fastMultiplier = 3.0f;
    public float scrollSpeed   = 2.0f;

    private float _yaw;
    private float _pitch;

    void Start()
    {
        // Initialize from current rotation so camera doesn't snap
        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // Only control camera while right mouse button is held
        // so you can still use Unity UI normally
        if (Input.GetMouseButton(1))
        {
            // Lock cursor while looking
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;

            _yaw   += Input.GetAxis("Mouse X") * lookSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
            _pitch  = Mathf.Clamp(_pitch, -89f, 89f);

            transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // Movement
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= fastMultiplier;

        // Scroll to adjust base speed on the fly
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        moveSpeed = Mathf.Clamp(moveSpeed + scroll * scrollSpeed, 0.5f, 50f);

        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    dir += transform.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  dir -= transform.forward;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  dir -= transform.right;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir += transform.right;
        if (Input.GetKey(KeyCode.E))                                      dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q))                                      dir -= Vector3.up;

        transform.position += dir * speed * Time.deltaTime;
    }
}

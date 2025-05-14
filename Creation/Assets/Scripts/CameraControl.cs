using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FreeFlyCamera : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 10f;            // 基础移动速度
    public float boostMultiplier = 2f;       // 按住 Shift 加速倍数

    [Header("鼠标参数")]
    public float mouseSensitivity = 3f;      // 鼠标灵敏度
    public bool invertY = false;             // 是否反转垂直视角

    private float yaw = 0f;                  // 水平旋转累计
    private float pitch = 0f;                // 垂直旋转累计

    void Start()
    {
        // 初始锁定并隐藏光标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 初始化 yaw/pitch 为当前朝向
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        // ESC 释放光标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw   += mouseX;
        pitch += invertY ? mouseY : -mouseY;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        // 键盘输入
        float forward  = Input.GetAxis("Vertical");    // W/S
        float right    = Input.GetAxis("Horizontal");  // A/D
        float up       = 0f;
        if (Input.GetKey(KeyCode.Q)) up += 1f;
        if (Input.GetKey(KeyCode.E)) up -= 1f;

        // 加速
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);

        // 计算移动向量（相对于摄像机本地坐标系）
        Vector3 dir = transform.forward * forward
                    + transform.right   * right
                    + transform.up      * up;

        transform.position += dir * speed * Time.deltaTime;
    }
}
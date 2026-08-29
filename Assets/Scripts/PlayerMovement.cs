using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMovement : MonoBehaviour
{
    public float jumpForce = 7f;
    private Rigidbody rb;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    
    public void OnJump(InputValue value)
    {
        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.VelocityChange
        );
    }
}
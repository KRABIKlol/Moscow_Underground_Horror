using UnityEngine;

public class VisitorMovement : MonoBehaviour
{
    public Transform pointB; // Точка, куда он идет
    public float moveSpeed = 2f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (pointB == null) return;

        // Двигаем через Rigidbody (правильно для физики)
        Vector3 direction = (pointB.position - transform.position).normalized;
        rb.linearVelocity = new Vector3(direction.x * moveSpeed, rb.linearVelocity.y, direction.z * moveSpeed);

        // Поворот в сторону движения
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
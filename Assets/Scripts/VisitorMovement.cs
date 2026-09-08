using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простое перемещение персонажа по точкам маршрута (например: спавн -> сканер -> выход).
/// Не использует NavMesh — подходит для прямого коридора вроде вашего чек-поинта.
/// Если нужен обход препятствий/сложные маршруты — лучше NavMeshAgent, это отдельная история.
/// </summary>
public class VisitorMovement : MonoBehaviour
{
    [Header("Маршрут")]
    [Tooltip("Точки, через которые пройдёт персонаж по порядку. Можно оставить одну точку — просто дойдёт до неё.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Header("Движение")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 8f;
    [Tooltip("На каком расстоянии до точки считаем, что дошли")]
    [SerializeField] private float arrivalThreshold = 0.15f;

    [Header("По завершении маршрута")]
    [SerializeField] private bool destroyAtEnd = true;
    [Tooltip("Если не уничтожать — можно зациклить маршрут (пойдёт заново с первой точки)")]
    [SerializeField] private bool loopRoute = false;

    [Header("Анимация (опционально)")]
    [Tooltip("Если есть Animator с параметром float Speed для блендинга Idle/Walk")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";

    /// <summary>Вызывается один раз, когда персонаж прошёл весь маршрут (перед уничтожением, если оно включено).</summary>
    public event Action OnRouteFinished;

    private int currentIndex = 0;
    private bool routeFinished = false;

    private void Update()
    {
        if (routeFinished || waypoints.Count == 0) return;

        Transform target = waypoints[currentIndex];
        if (target == null)
        {
            AdvanceToNextWaypoint();
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f; // не наклоняем персонажа вверх/вниз, если точки на разной высоте

        float distance = direction.magnitude;

        if (distance <= arrivalThreshold)
        {
            AdvanceToNextWaypoint();
            return;
        }

        Vector3 moveDir = direction.normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (animator != null)
            animator.SetFloat(speedParam, moveSpeed);
    }

    private void AdvanceToNextWaypoint()
    {
        currentIndex++;

        if (currentIndex >= waypoints.Count)
        {
            if (loopRoute)
            {
                currentIndex = 0;
                return;
            }

            FinishRoute();
        }
    }

    private void FinishRoute()
    {
        routeFinished = true;

        if (animator != null)
            animator.SetFloat(speedParam, 0f);

        OnRouteFinished?.Invoke();

        if (destroyAtEnd)
            Destroy(gameObject);
    }
}

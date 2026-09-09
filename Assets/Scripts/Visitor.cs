using System;
using System.Collections.Generic;
using UnityEngine;

/// Посетитель КПП: ходит к точкам, носит документ и вещи, крутит анимации.
public class Visitor : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 1.4f;
    public float turnSpeed = 540f;
    public float arriveDistance = 0.07f;

    [Header("Animation")]
    public Animator animator;
    [Tooltip("Float parameter driven by current speed.")]
    public string speedParam = "Speed";

    [Header("Data")]
    public DocumentData document;
    public List<ItemDefinition> items = new List<ItemDefinition>();

    public bool HasBanned => items.Exists(i => i != null && i.banned);

    /// Сработает ли рамка - только металл среди запрещённого.
    public bool DetectorTriggers => items.Exists(i => i != null && i.banned && i.triggersDetector);

    /// Правильное решение охранника: документ в порядке И нет запрещённого.
    public bool ShouldBeAllowed => document != null && document.valid && !HasBanned;

    public bool IsMoving { get; private set; }

    Vector3 _target;
    Quaternion? _finalRot;
    Action _onArrive;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
    }

    public void SetupAnimator(RuntimeAnimatorController controller, bool overrideExisting)
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!animator || !controller) return;

        if (animator.runtimeAnimatorController == null || overrideExisting)
            animator.runtimeAnimatorController = controller;

        animator.applyRootMotion = false;
    }

    public void PlayTrigger(string trigger)
    {
        if (!animator || string.IsNullOrEmpty(trigger)) return;
        if (!animator.runtimeAnimatorController) return;
        if (!HasParam(trigger, AnimatorControllerParameterType.Trigger)) return;
        animator.SetTrigger(trigger);
    }

    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        foreach (var p in animator.parameters)
            if (p.type == type && p.name == name) return true;
        return false;
    }

    public void GoTo(Transform t, Action onArrive = null)
    {
        if (!t) { onArrive?.Invoke(); return; }
        _target = t.position;
        _finalRot = t.rotation;
        _onArrive = onArrive;
        IsMoving = true;
    }

    void Update()
    {
        if (IsMoving)
        {
            if (Step(_target))
            {
                IsMoving = false;
                var cb = _onArrive;
                _onArrive = null;
                cb?.Invoke();
            }
            return;
        }

        if (_finalRot.HasValue)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, _finalRot.Value, turnSpeed * Time.deltaTime);

        SetAnimSpeed(0f);
    }

    bool Step(Vector3 target)
    {
        Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
        Vector3 delta = flat - transform.position;
        float dist = delta.magnitude;

        if (dist <= arriveDistance) { SetAnimSpeed(0f); return true; }

        Vector3 dir = delta / dist;
        transform.position += dir * Mathf.Min(moveSpeed * Time.deltaTime, dist);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        SetAnimSpeed(moveSpeed);
        return false;
    }

    void SetAnimSpeed(float v)
    {
        if (!animator || string.IsNullOrEmpty(speedParam)) return;
        if (!animator.runtimeAnimatorController) return;
        if (!HasParam(speedParam, AnimatorControllerParameterType.Float)) return;
        animator.SetFloat(speedParam, v, 0.1f, Time.deltaTime);
    }
}

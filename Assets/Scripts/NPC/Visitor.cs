using System;
using System.Collections.Generic;
using UnityEngine;

public class Visitor : MonoBehaviour
{
    
    public float moveSpeed = 1.4f;
    public float turnSpeed = 540f;
    public float arriveDistance = 0.07f;
    public float waypointTolerance = 0.3f;

    
    public Animator animator;
    
    public string speedParam = "Speed";

    
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    
    public float groundRayUp = 0.3f;
    
    public float groundRayDown = 4f;
   
    public float footOffset = 0f;

    
    public bool autoAddCollider = true;
    public float colliderHeight = 1.8f;
    public float colliderRadius = 0.3f;

    public DocumentData document;
    public List<ItemDefinition> items = new List<ItemDefinition>();

    public bool HasBanned => items.Exists(i => i != null && i.banned);

    
    public bool DetectorTriggers => items.Exists(i => i != null && i.banned && i.triggersDetector);

    
    public bool ShouldBeAllowed => document != null && document.valid && !HasBanned;

    public bool IsMoving { get; private set; }

    readonly RaycastHit[] _groundHits = new RaycastHit[8];
    readonly List<Transform> _path = new List<Transform>();
    int _pathIndex;
    Quaternion? _finalRot;
    Action _onArrive;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;

        if (autoAddCollider && GetComponentInChildren<Collider>() == null)
        {
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;              
            cap.height = colliderHeight;
            cap.radius = colliderRadius;
            cap.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
        }
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

    public void GoTo(Transform destination, Action onArrive = null)
        => GoVia(null, destination, onArrive);

  
    public void GoVia(IReadOnlyList<Transform> waypoints, Transform destination, Action onArrive = null)
    {
        _path.Clear();

        if (waypoints != null)
            foreach (var w in waypoints)
                if (w) _path.Add(w);

        if (destination) _path.Add(destination);

        if (_path.Count == 0) { onArrive?.Invoke(); return; }

        _pathIndex = 0;
        _finalRot = destination ? destination.rotation : (Quaternion?)null;
        _onArrive = onArrive;
        IsMoving = true;
    }

    void Update()
    {
        if (IsMoving)
        {
            while (_pathIndex < _path.Count && !_path[_pathIndex]) _pathIndex++;

            if (_pathIndex >= _path.Count) { Arrive(); return; }

            bool last = _pathIndex == _path.Count - 1;
            float tolerance = last ? arriveDistance : waypointTolerance;

            if (Step(_path[_pathIndex].position, tolerance))
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count) Arrive();
            }
            return;
        }

        if (_finalRot.HasValue)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, _finalRot.Value, turnSpeed * Time.deltaTime);

        SetAnimSpeed(0f);
    }

    public void Detain(Vector3 position, Quaternion rotation)
    {
        _path.Clear();
        _pathIndex = 0;
        _onArrive = null;
        IsMoving = false;

        transform.SetPositionAndRotation(position, rotation);
        _finalRot = rotation;
    }

    void LateUpdate()
    {
        if (!snapToGround) return;

        Vector3 origin = transform.position + Vector3.up * groundRayUp;
        float dist = groundRayUp + groundRayDown;

        int n = Physics.RaycastNonAlloc(new Ray(origin, Vector3.down), _groundHits, dist,
                                        groundMask, QueryTriggerInteraction.Ignore);

        float bestY = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < n; i++)
        {
            var h = _groundHits[i];
            if (!h.collider) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;  

            if (h.point.y > bestY) { bestY = h.point.y; found = true; }
        }

        if (!found) return;

        Vector3 p = transform.position;
        p.y = bestY + footOffset;
        transform.position = p;
    }

    void Arrive()
    {
        IsMoving = false;
        _path.Clear();
        var cb = _onArrive;
        _onArrive = null;
        cb?.Invoke();
    }

    bool Step(Vector3 target, float tolerance)
    {
        Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
        Vector3 delta = flat - transform.position;
        float dist = delta.magnitude;

        if (dist <= tolerance) { SetAnimSpeed(0f); return true; }

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

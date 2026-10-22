using System.Collections.Generic;
using UnityEngine;
using System;
using SojaExiles;
using UnityEngine.InputSystem;

public class PlayerDoorInteraction : MonoBehaviour
{
    public static PlayerDoorInteraction ActivePlayer { get; private set; }

    [SerializeField] private float interactDistance = 2f;
    [SerializeField, Range(-1f, 1f)] private float facingThreshold = 0.3f;
    [SerializeField] private bool useCameraForward = false;
    [SerializeField] private string openState = "Opening";
    [SerializeField] private string closeState = "Closing";
    [SerializeField] private float cooldown = 0.6f;

    private readonly Dictionary<Animator, bool> isOpen = new Dictionary<Animator, bool>();
    private readonly Dictionary<Animator, float> nextAllowed = new Dictionary<Animator, float>();

    private void Awake()
    {
        // This component was also placed on some door-frame objects. Only the
        // actual player should scan for doors and consume the interaction key.
        bool belongsToPlayer = GetComponent<HeroMovement>() != null ||
                               gameObject.name.Equals("hero", StringComparison.OrdinalIgnoreCase) ||
                               gameObject.name.Equals("player", StringComparison.OrdinalIgnoreCase);

        if (!belongsToPlayer || (ActivePlayer != null && ActivePlayer != this))
        {
            enabled = false;
            return;
        }

        ActivePlayer = this;
    }

    private void OnDestroy()
    {
        if (ActivePlayer == this)
            ActivePlayer = null;
    }

    private void Start()
    {
        Debug.Log("PlayerDoorInteraction VERSI BARU aktif");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        Vector3 origin = transform.position;
        Vector3 fwd = (useCameraForward && Camera.main != null) ? Camera.main.transform.forward : transform.forward;
        fwd.y = 0f;
        fwd.Normalize();

        Collider[] cols = Physics.OverlapSphere(origin, interactDistance, ~0, QueryTriggerInteraction.Collide);

        Animator best = null;
        float bestScore = float.MinValue;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;

            Animator anim = col.GetComponentInParent<Animator>();
            if (anim == null || !anim.HasState(0, Animator.StringToHash(openState))) continue;

            Vector3 to = col.bounds.ClosestPoint(origin) - origin;
            to.y = 0f;
            float dist = to.magnitude;
            float dot = dist < 0.01f ? 1f : Vector3.Dot(fwd, to / dist);

            if (dot < facingThreshold) continue;

            float score = dot - dist * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                best = anim;
            }
        }

        if (best != null) ToggleDoor(best);
        else Debug.Log("Tidak ada pintu di depan hero dalam jarak " + interactDistance);
    }

    private void ToggleDoor(Animator anim)
    {
        if (nextAllowed.TryGetValue(anim, out float t) && Time.time < t) return;

        opencloseDoor doorController = anim.GetComponent<opencloseDoor>();
        bool open = doorController != null
            ? doorController.open
            : isOpen.TryGetValue(anim, out bool rememberedOpen) && rememberedOpen;
        open = !open;
        isOpen[anim] = open;
        nextAllowed[anim] = Time.time + cooldown;

        // Keep the door script state in sync. Its own key polling is disabled
        // when the player interaction controller is active.
        if (doorController != null)
        {
            doorController.SetOpen(open);
            return;
        }

        anim.Play(open ? openState : closeState, 0, 0f);
        Debug.Log("Pintu '" + anim.name + "' -> " + (open ? "BUKA" : "TUTUP"));
    }
}

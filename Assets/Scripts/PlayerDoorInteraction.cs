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

    [Header("Kunci Pintu Boss Room")]
    [SerializeField] private string bossRoomDoorFrameName = "DoorFrame_Apt_01 (2)";
    [SerializeField] private string requiredKeyName = "Kunci Locker Room";
    [SerializeField] private string bossRoomCode = "1345";
    [SerializeField, Min(0.5f)] private float bossDoorPromptDistance = 2.5f;

    private readonly Dictionary<Animator, bool> isOpen = new Dictionary<Animator, bool>();
    private readonly Dictionary<Animator, float> nextAllowed = new Dictionary<Animator, float>();
    private Transform bossRoomDoorFrame;
    private Animator bossRoomDoorAnimator;
    private opencloseDoor bossRoomDoorController;
    private BoxCollider bossRoomAccessBlocker;
    private Inventory inventory;
    private bool codeEntryOpen;
    private bool codeFieldFocused;
    private string enteredCode = string.Empty;
    private string doorMessage = string.Empty;
    private bool heroNearBossRoomDoor;
    private bool codeDismissedUntilLeave;
    private bool bossRoomEntryNotified;
    private bool bossRoomDoorAutoCloseScheduled;
    private bool warnedBossDoorMissing;
    private float nextBossDoorResolveTime;
    private float doorMessageUntil;
    private float bossRoomDoorAutoCloseAt;
    private float previousTimeScale = 1f;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisibility;

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
        inventory = GetComponentInParent<Inventory>() ?? GetComponentInChildren<Inventory>(true);
    }

    private void OnDestroy()
    {
        if (codeEntryOpen)
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisibility;
        }

        if (ActivePlayer == this)
            ActivePlayer = null;
        if (bossRoomAccessBlocker != null)
        {
            Destroy(bossRoomAccessBlocker.gameObject);
        }
    }

    private void Start()
    {
        ResolveBossRoomDoor();
        CreateBossRoomAccessBlocker();
        Debug.Log("PlayerDoorInteraction VERSI BARU aktif");
    }

    private void Update()
    {
        if (bossRoomDoorAutoCloseScheduled && Time.time >= bossRoomDoorAutoCloseAt)
        {
            bossRoomDoorAutoCloseScheduled = false;
            CloseBossRoomDoor();
        }

        if (codeEntryOpen)
        {
            Keyboard modalKeyboard = Keyboard.current;
            if (modalKeyboard != null)
            {
                if (modalKeyboard.xKey.wasPressedThisFrame || modalKeyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseCodeEntry();
                }
                else if (modalKeyboard.enterKey.wasPressedThisFrame || modalKeyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    SubmitRoomCode();
                }
            }
            return;
        }

        if (InventoryUI.IsOpen)
        {
            heroNearBossRoomDoor = false;
            return;
        }

        if (bossRoomDoorAnimator == null && Time.unscaledTime >= nextBossDoorResolveTime)
        {
            nextBossDoorResolveTime = Time.unscaledTime + 1f;
            ResolveBossRoomDoor();
            CreateBossRoomAccessBlocker();
        }

        heroNearBossRoomDoor = IsHeroInFrontOfBossRoomDoor();
        NotifyBossWhenHeroEntersRoom();
        if (!heroNearBossRoomDoor)
        {
            codeDismissedUntilLeave = false;
        }

        if (heroNearBossRoomDoor && HasRequiredKey() && !codeDismissedUntilLeave)
        {
            OpenCodeEntry();
            return;
        }

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

        if (best != null && IsBossRoomDoor(best))
        {
            if (HasRequiredKey())
            {
                OpenCodeEntry();
            }
            else
            {
                doorMessage = "Pintu terkunci. Butuh " + requiredKeyName + ".";
                doorMessageUntil = Time.unscaledTime + 2.5f;
            }
        }
        else if (best != null) ToggleDoor(best);
        else Debug.Log("Tidak ada pintu di depan hero dalam jarak " + interactDistance);
    }

    private bool bossRoomUnlocked;

    private void ResolveBossRoomDoor()
    {
        // Find every scene object with this name. GameObject.Find can return a
        // duplicate frame that has no door leaf/Animator, even when the real
        // animated door is present elsewhere in the loaded scene.
        if (bossRoomDoorFrame == null || bossRoomDoorAnimator == null)
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform bestFrame = null;
            Animator bestAnimator = null;
            float bestDistance = float.PositiveInfinity;
            Transform closestNamedFrame = null;
            float closestFrameDistance = float.PositiveInfinity;
            Animator[] sceneAnimators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            opencloseDoor[] sceneDoorControllers = FindObjectsByType<opencloseDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform candidateFrame in sceneTransforms)
            {
                if (candidateFrame == null || candidateFrame.name != bossRoomDoorFrameName || !candidateFrame.gameObject.scene.IsValid())
                    continue;

                float frameDistance = Vector3.Distance(transform.position, candidateFrame.position);
                if (frameDistance < closestFrameDistance)
                {
                    closestNamedFrame = candidateFrame;
                    closestFrameDistance = frameDistance;
                }

                opencloseDoor candidateController = candidateFrame.GetComponent<opencloseDoor>() ??
                    candidateFrame.GetComponentInChildren<opencloseDoor>(true);
                Animator candidateAnimator = candidateController != null ? candidateController.openandclose : null;
                candidateAnimator = candidateAnimator ?? candidateFrame.GetComponent<Animator>() ??
                    candidateFrame.GetComponentInChildren<Animator>(true);
                if (candidateAnimator == null)
                {
                    // Some scene variants put the animated door leaf beside the
                    // frame. Accept a nearby animated leaf, but never steal the
                    // PatrolPoint 3 door (DoorFrame_Apt_01 (1)).
                    foreach (Animator possibleDoor in sceneAnimators)
                    {
                        if (possibleDoor == null || !possibleDoor.gameObject.scene.IsValid() ||
                            IsPatrolThreeDoor(possibleDoor.transform))
                            continue;
                        float distance = Vector3.Distance(candidateFrame.position, possibleDoor.transform.position);
                        if (distance <= 4f && distance < bestDistance && possibleDoor.HasState(0, Animator.StringToHash(openState)))
                        {
                            bestFrame = candidateFrame;
                            bestAnimator = possibleDoor;
                            bestDistance = distance;
                        }
                    }

                    foreach (opencloseDoor possibleController in sceneDoorControllers)
                    {
                        if (possibleController == null || !possibleController.gameObject.scene.IsValid() ||
                            IsPatrolThreeDoor(possibleController.transform))
                            continue;

                        Animator possibleAnimator = possibleController.openandclose ??
                            possibleController.GetComponent<Animator>() ??
                            possibleController.GetComponentInChildren<Animator>(true);
                        float distance = Vector3.Distance(candidateFrame.position, possibleController.transform.position);
                        if (possibleAnimator != null && distance <= 4f && distance < bestDistance &&
                            possibleAnimator.HasState(0, Animator.StringToHash(openState)))
                        {
                            bestFrame = candidateFrame;
                            bestAnimator = possibleAnimator;
                            bestDistance = distance;
                        }
                    }
                    continue;
                }

                if (candidateAnimator.HasState(0, Animator.StringToHash(openState)) && frameDistance < bestDistance)
                {
                    bestFrame = candidateFrame;
                    bestAnimator = candidateAnimator;
                    bestDistance = frameDistance;
                }
            }

            if (bestFrame != null)
                bossRoomDoorFrame = bestFrame;
            else if (closestNamedFrame != null)
                bossRoomDoorFrame = closestNamedFrame;
            if (bestAnimator != null)
                bossRoomDoorAnimator = bestAnimator;
        }

        if (bossRoomDoorFrame == null)
        {
            if (!warnedBossDoorMissing)
            {
                Debug.LogWarning("PlayerDoorInteraction: frame pintu Boss Room '" + bossRoomDoorFrameName + "' tidak ditemukan.", this);
                warnedBossDoorMissing = true;
            }
            return;
        }

        bossRoomDoorController = bossRoomDoorFrame.GetComponent<opencloseDoor>() ??
                                 bossRoomDoorFrame.GetComponentInChildren<opencloseDoor>(true);
        bossRoomDoorAnimator = bossRoomDoorAnimator ?? bossRoomDoorFrame.GetComponent<Animator>() ??
                               bossRoomDoorFrame.GetComponentInChildren<Animator>(true);

        if (bossRoomDoorAnimator == null && bossRoomDoorController != null)
        {
            bossRoomDoorAnimator = bossRoomDoorController.openandclose ??
                                   bossRoomDoorController.GetComponent<Animator>() ??
                                   bossRoomDoorController.GetComponentInChildren<Animator>(true);
        }

        if (bossRoomDoorAnimator == null)
        {
            Animator nearestDoorAnimator = null;
            float nearestDoorDistance = 4f;
            Animator[] sceneAnimators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Animator candidate in sceneAnimators)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                    IsPatrolThreeDoor(candidate.transform) || !candidate.HasState(0, Animator.StringToHash(openState)))
                    continue;

                float distance = Vector3.Distance(bossRoomDoorFrame.position, candidate.transform.position);
                if (distance < nearestDoorDistance)
                {
                    nearestDoorAnimator = candidate;
                    nearestDoorDistance = distance;
                }
            }

            bossRoomDoorAnimator = nearestDoorAnimator;
        }

        if (bossRoomDoorAnimator == null)
        {
            if (!warnedBossDoorMissing)
            {
                Debug.LogWarning("PlayerDoorInteraction: Animator pintu " + bossRoomDoorFrameName + " tidak ditemukan; UI kode tetap dapat dipakai.", this);
                warnedBossDoorMissing = true;
            }
            return;
        }

        warnedBossDoorMissing = false;

        bossRoomDoorController = bossRoomDoorAnimator.GetComponent<opencloseDoor>() ??
                                 bossRoomDoorAnimator.GetComponentInParent<opencloseDoor>() ??
                                 bossRoomDoorAnimator.GetComponentInChildren<opencloseDoor>(true) ??
                                 bossRoomDoorController;
        if (bossRoomDoorController != null && bossRoomDoorController.openandclose == null)
        {
            bossRoomDoorController.openandclose = bossRoomDoorAnimator;
        }

        isOpen[bossRoomDoorAnimator] = bossRoomDoorController != null && bossRoomDoorController.open;
    }

    private static bool IsPatrolThreeDoor(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            if (current.name == "DoorFrame_Apt_01 (1)")
            {
                return true;
            }
        }

        return false;
    }

    private void CreateBossRoomAccessBlocker()
    {
        if (bossRoomDoorFrame == null || bossRoomAccessBlocker != null)
        {
            return;
        }

        BoxCollider doorLeaf = null;
        BoxCollider[] doorColliders = bossRoomDoorFrame.GetComponentsInChildren<BoxCollider>(true);
        foreach (BoxCollider candidate in doorColliders)
        {
            if (candidate != null && (doorLeaf == null || candidate.size.y * Mathf.Abs(candidate.transform.lossyScale.y) >
                doorLeaf.size.y * Mathf.Abs(doorLeaf.transform.lossyScale.y)))
            {
                doorLeaf = candidate;
            }
        }

        if (doorLeaf == null && bossRoomDoorAnimator != null)
        {
            doorLeaf = bossRoomDoorAnimator.GetComponent<BoxCollider>() ??
                       bossRoomDoorAnimator.GetComponentInChildren<BoxCollider>(true);
        }

        if (doorLeaf == null)
        {
            Debug.LogWarning("PlayerDoorInteraction: collider daun pintu Boss Room tidak ditemukan; akses terkunci tidak bisa dibuat.", this);
            return;
        }

        GameObject blockerObject = new GameObject("BossRoom_HeroAccessBlocker", typeof(BoxCollider));
        blockerObject.transform.SetPositionAndRotation(
            doorLeaf.transform.TransformPoint(doorLeaf.center),
            doorLeaf.transform.rotation);
        Vector3 leafScale = doorLeaf.transform.lossyScale;
        blockerObject.transform.localScale = new Vector3(
            Mathf.Abs(leafScale.x), Mathf.Abs(leafScale.y), Mathf.Abs(leafScale.z));
        bossRoomAccessBlocker = blockerObject.GetComponent<BoxCollider>();
        bossRoomAccessBlocker.size = new Vector3(
            Mathf.Abs(doorLeaf.size.x), Mathf.Abs(doorLeaf.size.y), Mathf.Abs(doorLeaf.size.z));

        Collider[] heroColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider heroCollider in heroColliders)
        {
            if (heroCollider != null && !heroCollider.isTrigger)
            {
                Physics.IgnoreCollision(heroCollider, bossRoomAccessBlocker, false);
            }
        }

        // Boss tetap boleh membuka dan melewati pintu untuk jalur patrolnya;
        // collider pengunci ini hanya menahan Hero sebelum memasukkan kode.
        BossController[] bosses = FindObjectsByType<BossController>(FindObjectsSortMode.None);
        foreach (BossController boss in bosses)
        {
            if (boss == null)
            {
                continue;
            }

            Collider[] bossColliders = boss.GetComponentsInChildren<Collider>(true);
            foreach (Collider bossCollider in bossColliders)
            {
                if (bossCollider != null && !bossCollider.isTrigger)
                {
                    Physics.IgnoreCollision(bossCollider, bossRoomAccessBlocker, true);
                }
            }
        }
    }

    private bool IsBossRoomDoor(Animator animator)
    {
        if (animator == null || bossRoomDoorFrame == null)
        {
            return false;
        }

        return animator == bossRoomDoorAnimator || animator.transform == bossRoomDoorFrame ||
               animator.transform.IsChildOf(bossRoomDoorFrame);
    }

    private bool IsHeroInFrontOfBossRoomDoor()
    {
        if (bossRoomDoorFrame == null ||
            Vector3.Distance(transform.position, bossRoomDoorFrame.position) > bossDoorPromptDistance)
        {
            return false;
        }

        Vector3 toDoor = bossRoomDoorFrame.position - transform.position;
        toDoor.y = 0f;
        Vector3 forward = (useCameraForward && Camera.main != null)
            ? Camera.main.transform.forward
            : transform.forward;
        forward.y = 0f;
        return toDoor.sqrMagnitude < 0.001f ||
               Vector3.Dot(forward.normalized, toDoor.normalized) >= facingThreshold;
    }

    private bool HasRequiredKey()
    {
        if (inventory == null)
        {
            inventory = GetComponentInParent<Inventory>() ?? GetComponentInChildren<Inventory>(true);
        }

        if (inventory == null)
        {
            return false;
        }

        foreach (InventorySlot slot in inventory.slots)
        {
            if (slot != null && slot.item != null && slot.amount > 0 &&
                slot.item.itemType == ItemType.Key &&
                string.Equals(slot.item.itemName, requiredKeyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OpenCodeEntry()
    {
        if (bossRoomUnlocked || bossRoomDoorFrame == null)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        enteredCode = string.Empty;
        doorMessage = string.Empty;
        codeFieldFocused = false;
        codeEntryOpen = true;
    }

    private void SubmitRoomCode()
    {
        if (string.Equals(enteredCode.Trim(), bossRoomCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // The code panel pauses normal door discovery updates. Refresh
            // references here so a late-loaded/previously unresolved Animator
            // does not leave the valid code unable to open the door.
            ResolveBossRoomDoor();

            if (bossRoomDoorAnimator == null)
            {
                doorMessage = "Animator pintu Boss Room belum ditemukan. Pastikan komponen Animator terpasang pada daun pintu.";
                doorMessageUntil = Time.unscaledTime + 4f;
                return;
            }

            if (bossRoomDoorController != null && bossRoomDoorController.openandclose == null)
            {
                bossRoomDoorController.openandclose = bossRoomDoorAnimator;
            }

            bossRoomUnlocked = true;
            if (bossRoomAccessBlocker != null)
            {
                bossRoomAccessBlocker.enabled = false;
            }

            if (bossRoomDoorController != null)
            {
                bossRoomDoorController.SetOpen(true);
            }
            else
            {
                isOpen[bossRoomDoorAnimator] = true;
                bossRoomDoorAnimator.Play(openState, 0, 0f);
            }

            bossRoomDoorAutoCloseScheduled = true;
            bossRoomDoorAutoCloseAt = Time.time + 5f;
            doorMessage = string.Empty;
            CloseCodeEntry();
            return;
        }

        enteredCode = string.Empty;
        doorMessage = "Kode salah. Coba lagi.";
    }

    private void NotifyBossWhenHeroEntersRoom()
    {
        if (!bossRoomUnlocked || bossRoomEntryNotified || bossRoomDoorFrame == null)
        {
            return;
        }

        BossPatrolWaypoint[] waypoints = FindObjectsByType<BossPatrolWaypoint>(FindObjectsSortMode.None);
        Transform bossRoomWaypoint = null;
        foreach (BossPatrolWaypoint waypoint in waypoints)
        {
            if (waypoint != null && waypoint.name.IndexOf("BossRoom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bossRoomWaypoint = waypoint.transform;
                break;
            }
        }

        if (bossRoomWaypoint == null)
        {
            return;
        }

        Vector3 doorNormal = bossRoomDoorFrame.forward;
        doorNormal.y = 0f;
        if (doorNormal.sqrMagnitude < 0.001f)
        {
            doorNormal = bossRoomDoorFrame.right;
            doorNormal.y = 0f;
        }
        doorNormal.Normalize();
        Vector3 toBossRoom = bossRoomWaypoint.position - bossRoomDoorFrame.position;
        toBossRoom.y = 0f;
        Vector3 toHero = transform.position - bossRoomDoorFrame.position;
        toHero.y = 0f;
        float insideSide = Mathf.Sign(Vector3.Dot(doorNormal, toBossRoom));
        float heroSide = Vector3.Dot(doorNormal, toHero) * insideSide;

        // Confirm the Hero has crossed the doorway and moved clear of its frame.
        // The waypoint proximity check covers door prefabs whose frame forward
        // axis does not line up with the opening.
        bool crossedDoor = insideSide != 0f && heroSide > 0.15f &&
                           Vector3.Distance(transform.position, bossRoomDoorFrame.position) >= 1.1f;
        bool reachedInsidePoint = HorizontalDistance(transform.position, bossRoomWaypoint.position) <= 1.5f;
        if (!crossedDoor && !reachedInsidePoint)
        {
            return;
        }

        bossRoomEntryNotified = true;
        BossController[] bosses = FindObjectsByType<BossController>(FindObjectsSortMode.None);
        foreach (BossController boss in bosses)
        {
            if (boss != null)
            {
                boss.NotifyHeroEnteredBossRoom();
            }
        }
    }

    private void CloseBossRoomDoor()
    {
        ResolveBossRoomDoor();
        if (bossRoomDoorController != null)
        {
            if (bossRoomDoorController.openandclose == null)
            {
                bossRoomDoorController.openandclose = bossRoomDoorAnimator;
            }

            if (bossRoomDoorController.open)
            {
                bossRoomDoorController.SetOpen(false);
            }
            else if (bossRoomDoorAnimator != null)
            {
                bossRoomDoorAnimator.Play(closeState, 0, 0f);
            }
        }
        else if (bossRoomDoorAnimator != null)
        {
            bossRoomDoorAnimator.Play(closeState, 0, 0f);
        }
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private void CloseCodeEntry()
    {
        codeEntryOpen = false;
        codeDismissedUntilLeave = !bossRoomUnlocked;
        doorMessage = string.Empty;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisibility;
    }

    private void OnGUI()
    {
        if (codeEntryOpen)
        {
            DrawCodeEntryUI();
            return;
        }

        if (heroNearBossRoomDoor && !HasRequiredKey())
        {
            DrawDoorMessage("PINTU TERKUNCI", "Butuh " + requiredKeyName + " dari Locker Room.");
        }
        else if (!string.IsNullOrEmpty(doorMessage) && Time.unscaledTime <= doorMessageUntil)
        {
            DrawDoorMessage("AKSES DITOLAK", doorMessage);
        }
    }

    private void DrawCodeEntryUI()
    {
        const float width = 470f;
        const float height = 300f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = Color.white;
        GUI.Box(panel, GUIContent.none);
        if (GUI.Button(new Rect(panel.xMax - 48f, panel.y + 10f, 36f, 30f), "X"))
        {
            CloseCodeEntry();
            return;
        }

        GUILayout.BeginArea(new Rect(panel.x + 28f, panel.y + 22f, panel.width - 56f, panel.height - 44f));
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 25,
            fontStyle = FontStyle.Bold
        };
        title.normal.textColor = Color.white;
        GUIStyle subtitle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            wordWrap = true
        };
        subtitle.normal.textColor = new Color(0.72f, 0.79f, 0.84f, 1f);
        GUILayout.Label("AKSES RUANGAN BOSS", title, GUILayout.Height(42f));
        GUILayout.Label("Masukkan kode ruangan, lalu tekan Enter. Pintu terbuka jika kode benar.", subtitle, GUILayout.Height(42f));
        GUILayout.Space(14f);
        GUI.SetNextControlName("BossRoomCodeField");
        string typedCode = GUILayout.TextField(enteredCode, 4, GUILayout.Height(42f));
        enteredCode = string.Empty;
        foreach (char character in typedCode)
        {
            if (char.IsDigit(character) && enteredCode.Length < 4)
            {
                enteredCode += character;
            }
        }
        if (!codeFieldFocused)
        {
            GUI.FocusControl("BossRoomCodeField");
            codeFieldFocused = true;
        }
        GUILayout.Space(12f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("BUKA PINTU", GUILayout.Height(44f)))
        {
            SubmitRoomCode();
        }

        if (GUILayout.Button("BATAL", GUILayout.Height(44f)))
        {
            CloseCodeEntry();
        }
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(doorMessage))
        {
            GUIStyle message = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            message.normal.textColor = new Color(1f, 0.55f, 0.35f, 1f);
            GUILayout.Label(doorMessage, message, GUILayout.Height(26f));
        }
        GUILayout.EndArea();

    }

    private void DrawDoorMessage(string titleText, string messageText)
    {
        const float width = 430f;
        const float height = 86f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - 150f, width, height);
        GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.92f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 28f), titleText, style);
        GUIStyle detail = new GUIStyle(style)
        {
            fontSize = 14,
            fontStyle = FontStyle.Normal
        };
        detail.normal.textColor = new Color(0.82f, 0.85f, 0.88f, 1f);
        GUI.Label(new Rect(panel.x + 10f, panel.y + 35f, panel.width - 20f, 42f), messageText, detail);
        GUI.color = Color.white;
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

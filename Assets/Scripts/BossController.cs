using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.AI.Navigation;
using SojaExiles;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class BossController : MonoBehaviour
{
    private enum BossState
    {
        Idle,
        Patrol,
        Chase,
        Attack
    }

    [Header("References")]
    [Tooltip("Transform Player yang akan dikejar dan diserang Boss.")]
    [SerializeField] private Transform playerTarget;

    [Tooltip("NavMeshAgent menyimpan konfigurasi ukuran Boss. Gerak Boss memakai CharacterController.")]
    [SerializeField] private NavMeshAgent agent;

    [Tooltip("Character Controller untuk gerak yang tertahan dinding dan pintu.")]
    [SerializeField] private CharacterController movementController;

    [Tooltip("Animator pada model Boss.")]
    [SerializeField] private Animator anim;

    [Header("Boss Position")]
    [Tooltip("Titik awal Boss, misalnya child BossSpawnPoint pada komputer utama.")]
    [SerializeField] private Transform bossSpawnPoint;

    [Tooltip("Jika aktif, Boss dipindahkan ke Boss Spawn Point saat game dimulai.")]
    [SerializeField] private bool useBossSpawnPoint = true;

    [Header("Patrol")]
    [Tooltip("Urutkan titik: Boss Room, ruang tengah, Locker Room, lalu dapur. Boss kembali ke titik pertama setelah dapur.")]
    [SerializeField] private Transform[] patrolPoints;

    [SerializeField, Min(0f)] private float patrolPauseMinimum = 5f;
    [SerializeField, Min(0f)] private float patrolPauseMaximum = 7f;
    [SerializeField, Min(0f)] private float initialCountdown = 10f;

    [Tooltip("Kecepatan Boss menoleh saat mencari Player di titik patroli.")]
    [SerializeField, Min(0f)] private float lookAroundSpeed = 45f;

    [Tooltip("Jarak dari titik patroli agar Boss dianggap sudah tiba.")]
    [SerializeField, Min(0.05f)] private float patrolArrivalDistance = 0.5f;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float chaseDistance = 10f;
    [SerializeField, Min(0f)] private float attackDistance = 2f;
    [SerializeField, Range(1f, 360f)] private float scanFieldOfView = 110f;

    [Tooltip("Titik pandang Boss. Jika kosong, posisi Boss dengan offset ke atas akan digunakan.")]
    [SerializeField] private Transform eyePoint;

    [Tooltip("Tinggi titik pandang jika Eye Point tidak diisi.")]
    [SerializeField, Min(0f)] private float eyeHeight = 1.2f;

    [Header("Attack")]
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.5f;

    [Tooltip("Damage yang diterima Player setiap serangan Boss.")]
    [SerializeField, Min(0)] private int attackDamage = 10;

    [Header("Movement")]
    [Tooltip("Kecepatan gerak Boss saat mengejar Player.")]
    [SerializeField, Min(0f)] private float movementSpeed = 3.5f;

    [Tooltip("Percepatan NavMeshAgent Boss.")]
    [SerializeField, Min(0f)] private float movementAcceleration = 12f;

    [Tooltip("Radius navigasi Boss. Sesuaikan dengan lebar pintu agar jalur NavMesh tidak tertutup.")]
    [SerializeField, Range(0.2f, 0.5f)] private float navigationRadius = 0.3f;

    [Tooltip("Kecepatan putar NavMeshAgent Boss.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField] private bool createTimerUI = true;
    [SerializeField, Min(0.25f)] private float doorOpeningDistance = 2.2f;
    [SerializeField, Min(1f)] private float doorRouteLookAheadDistance = 4.5f;
    [SerializeField, Min(1f)] private float heartbeatEffectDistance = 8f;
    [SerializeField, Range(0f, 0.5f)] private float heartbeatEffectMaxAlpha = 0.2f;
    [Tooltip("Waktu tanpa perpindahan sebelum jalur dihitung ulang saat Boss tertahan.")]
    [SerializeField, Min(0.25f)] private float stuckRepathDelay = 1.25f;

    [Tooltip("Jarak berhenti Boss dari Player.")]
    [SerializeField, Min(0f)] private float stoppingDistance = 1.5f;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private BossState currentState = BossState.Idle;
    private float nextAttackTime;
    private bool warnedAboutPlayer;
    private int patrolPointIndex;
    private float patrolPauseEndsAt;
    private bool isWaitingAtPatrolPoint;
    private bool isWaitingForPatrolStart;
    private float patrolStartAt;
    private Coroutine pendingAttack;
    private Text bossTimerText;
    private Image heartbeatOverlay;
    private Transform patrolThreeDoorRoot;
    private float patrolThreeDoorReadyAt;
    private opencloseDoor[] animatedDoors = System.Array.Empty<opencloseDoor>();
    private Door[] scriptedDoors = System.Array.Empty<Door>();
    private readonly HashSet<opencloseDoor> bossOpenedAnimatedDoors = new HashSet<opencloseDoor>();
    private readonly HashSet<Door> bossOpenedScriptedDoors = new HashSet<Door>();
    private NavMeshSurface runtimeNavigationSurface;
    private bool navigationReady;
    private bool hasNavigationDestination;
    private Vector3 lastNavigationDestination;
    private Vector3 lastProgressPosition;
    private float lastProgressTime;

    private const string IsWalkingParameter = "isWalking";
    private const string AttackState = "m_melee_combat_attack_A";
    private const string IdleState = "m_idle_A";

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Animator childAnimator = GetComponentInChildren<Animator>();

            if (childAnimator != null && childAnimator.runtimeAnimatorController != null)
            {
                anim = childAnimator;
            }
        }

        maxHealth = Mathf.Max(1, maxHealth);
        chaseDistance = Mathf.Max(0f, chaseDistance);
        attackDistance = Mathf.Max(0f, attackDistance);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
        movementSpeed = Mathf.Max(0f, movementSpeed);
        movementAcceleration = Mathf.Max(0f, movementAcceleration);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);

        CurrentHealth = maxHealth;

        if (anim != null)
        {
            // Gerakan Boss dikendalikan langsung oleh Transform, tanpa root motion.
            anim.applyRootMotion = false;
        }

        ConfigureAgent();
        ConfigureMovementController();
        lastProgressPosition = transform.position;
        lastProgressTime = Time.time;
    }

    private void Start()
    {
        CreateTimerUI();
        CreateHeartbeatOverlay();
        CacheDoors();
        BuildRuntimeNavigation();
        MoveToBossSpawnPoint();
        isWaitingForPatrolStart = initialCountdown > 0f;
        patrolStartAt = Time.time + initialCountdown;
        isWaitingAtPatrolPoint = isWaitingForPatrolStart;
        patrolPauseEndsAt = patrolStartAt;
        if (playerTarget == null && !warnedAboutPlayer)
        {
            Debug.LogError(
                "BossController: Player Target belum diisi pada " + gameObject.name + ".",
                this);

            warnedAboutPlayer = true;
        }

        if (!HasValidAnimator())
        {
            Debug.LogWarning(
                "BossController: Animator Boss belum memiliki Animator Controller. " +
                "Pasang Controller pada field Controller di komponen Animator.",
                this);
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.speed = movementSpeed;
        agent.acceleration = movementAcceleration;
        agent.angularSpeed = rotationSpeed;
        agent.radius = navigationRadius;
        agent.stoppingDistance = stoppingDistance;
        // NavMeshAgent selalu menggerakkan Boss. Jangan alihkan jalur parsial
        // ke CharacterController karena collider pintu dapat menghentikannya.
        agent.updatePosition = true;
        agent.updateRotation = false;
    }

    private void MoveToBossSpawnPoint()
    {
        if (!useBossSpawnPoint || bossSpawnPoint == null)
        {
            return;
        }

        if (navigationReady && NavMesh.SamplePosition(
                bossSpawnPoint.position,
                out NavMeshHit spawnHit,
                3f,
                agent.areaMask))
        {
            if (agent.Warp(spawnHit.position))
            {
                // updatePosition=false berarti Warp tidak selalu memindahkan
                // Transform yang digerakkan CharacterController.
                transform.position = spawnHit.position;
                transform.rotation = bossSpawnPoint.rotation;
                agent.nextPosition = spawnHit.position;
                return;
            }
        }

        transform.SetPositionAndRotation(bossSpawnPoint.position, bossSpawnPoint.rotation);
    }

    private void BuildRuntimeNavigation()
    {
        if (agent == null)
        {
            return;
        }

        IgnoreDoorGeometryWhenBaking();

        runtimeNavigationSurface = FindAnyObjectByType<NavMeshSurface>();
        if (runtimeNavigationSurface == null)
        {
            GameObject surfaceObject = new GameObject("BossRuntimeNavigation");
            runtimeNavigationSurface = surfaceObject.AddComponent<NavMeshSurface>();
            runtimeNavigationSurface.agentTypeID = agent.agentTypeID;
            runtimeNavigationSurface.collectObjects = CollectObjects.Volume;
            runtimeNavigationSurface.layerMask = Physics.DefaultRaycastLayers;
            runtimeNavigationSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            runtimeNavigationSurface.ignoreNavMeshAgent = true;
            runtimeNavigationSurface.ignoreNavMeshObstacle = true;
        }

        // Hanya bake area permainan yang dilalui Boss. CollectObjects.All juga
        // memasukkan geometri aset yang jauh dari level, sehingga jumlah tile
        // NavMesh dapat melewati batas maksimum.
        Bounds navigationBounds = CalculateNavigationBounds();
        runtimeNavigationSurface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        runtimeNavigationSurface.collectObjects = CollectObjects.Volume;
        runtimeNavigationSurface.center = navigationBounds.center;
        runtimeNavigationSurface.size = navigationBounds.size;

        // Jangan biarkan agent aktif saat data NavMesh baru dipasang; agent
        // yang masih di posisi prefab sering memunculkan "not close enough".
        agent.enabled = false;

        // Selalu bangun ulang setelah modifier pintu dan radius agent diterapkan.
        // Memakai data bake lama dapat menyisakan pintu sebagai celah NavMesh.
        runtimeNavigationSurface.BuildNavMesh();

        Vector3 startPosition = useBossSpawnPoint && bossSpawnPoint != null
            ? bossSpawnPoint.position
            : transform.position;
        NavMeshHit startHit = default;
        navigationReady = runtimeNavigationSurface.navMeshData != null &&
                          NavMesh.SamplePosition(
                              startPosition,
                              out startHit,
                              3f,
                              agent.areaMask);

        if (navigationReady)
        {
            // Tempatkan Boss di permukaan sebelum mengaktifkan NavMeshAgent.
            transform.position = startHit.position;
            agent.enabled = true;
            agent.Warp(startHit.position);
        }

        if (!navigationReady)
        {
            // Tetap izinkan gerak CharacterController jika bake gagal.
            agent.enabled = false;
            Debug.LogWarning(
                "BossController: jalur NavMesh tidak dapat dibangun di scene ini. " +
                "Periksa apakah lantai dan dinding memiliki mesh yang dapat dipakai untuk navigasi.",
                this);
            return;
        }

        CreateDoorwayLinks();

        // Jalur lengkap digerakkan oleh NavMeshAgent supaya Boss mengikuti
        // koridor tanpa tertahan collider daun pintu.
        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.autoTraverseOffMeshLink = true;
        if (movementController != null)
        {
            movementController.enabled = false;
        }
    }

    private Bounds CalculateNavigationBounds()
    {
        Vector3 firstPoint = useBossSpawnPoint && bossSpawnPoint != null
            ? bossSpawnPoint.position
            : transform.position;
        Bounds bounds = new Bounds(firstPoint, Vector3.zero);
        bounds.Encapsulate(transform.position);

        if (playerTarget != null)
        {
            bounds.Encapsulate(playerTarget.position);
        }

        if (patrolPoints != null)
        {
            foreach (Transform point in patrolPoints)
            {
                if (point != null)
                {
                    bounds.Encapsulate(point.position);
                }
            }
        }

        // Sertakan seluruh ruangan dan koridor di antara waypoint, bukan hanya
        // volume kecil di sekeliling titiknya. Volume ini tetap membuang aset
        // proyek yang sangat jauh dari level (penyebab tile NavMesh berlebih).
        bounds.Expand(new Vector3(40f, 20f, 40f));
        return bounds;
    }

    private void CreateDoorwayLinks()
    {
        // Hubungkan NavMesh di kedua sisi pintu. Ini menangani bukaan yang putus
        // akibat dinding/kusen saat NavMesh dibangun, tanpa mengarahkan Boss
        // menembus dinding di samping pintu.
        for (int i = 0; i < animatedDoors.Length; i++)
        {
            opencloseDoor door = animatedDoors[i];
            if (door != null && IsBossSizedDoor(door.transform))
            {
                CreateDoorwayLink(door.transform);
            }
        }

        for (int i = 0; i < scriptedDoors.Length; i++)
        {
            Door door = scriptedDoors[i];
            if (door != null && IsBossSizedDoor(door.transform))
            {
                CreateDoorwayLink(door.transform);
            }
        }
    }

    private void CreateDoorwayLink(Transform doorRoot)
    {
        BoxCollider doorBox = null;
        Collider[] colliders = doorRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider doorCollider in colliders)
        {
            if (doorCollider is BoxCollider box && !box.isTrigger &&
                (doorBox == null || box.bounds.size.y > doorBox.bounds.size.y))
            {
                doorBox = box;
            }
        }

        if (doorBox == null)
        {
            return;
        }

        Vector3 crossingNormal;
        if (doorBox.size.x <= doorBox.size.z)
        {
            crossingNormal = doorBox.transform.right;
        }
        else
        {
            crossingNormal = doorBox.transform.forward;
        }

        crossingNormal.y = 0f;
        if (crossingNormal.sqrMagnitude < 0.001f)
        {
            return;
        }

        crossingNormal.Normalize();
        Bounds doorBounds = doorBox.bounds;
        Vector3 doorwayCenter = doorBounds.center;
        doorwayCenter.y = doorBounds.min.y + 0.1f;

        const float sideOffset = 0.9f;
        Vector3 sideA = doorwayCenter - crossingNormal * sideOffset;
        Vector3 sideB = doorwayCenter + crossingNormal * sideOffset;
        if (!NavMesh.SamplePosition(sideA, out NavMeshHit hitA, 0.65f, agent.areaMask) ||
            !NavMesh.SamplePosition(sideB, out NavMeshHit hitB, 0.65f, agent.areaMask) ||
            Vector3.Distance(hitA.position, hitB.position) < 1.1f)
        {
            return;
        }

        GameObject linkObject = new GameObject("BossDoorwayLink_" + doorRoot.name);
        linkObject.transform.position = doorwayCenter;
        NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
        link.agentTypeID = agent.agentTypeID;
        link.startPoint = linkObject.transform.InverseTransformPoint(hitA.position);
        link.endPoint = linkObject.transform.InverseTransformPoint(hitB.position);
        // Gunakan lebar bukaan pintu yang sebenarnya. Link 0.65 m sebelumnya
        // terlalu sempit untuk agent radius 0.3 m setelah toleransi NavMesh.
        link.width = Mathf.Max(
            doorBox.bounds.size.x,
            doorBox.bounds.size.z,
            agent.radius * 2f + 0.2f);
        link.bidirectional = true;
        link.costModifier = -1;
    }

    private void ConfigureMovementController()
    {
        if (movementController == null)
        {
            movementController = GetComponent<CharacterController>();
        }

        if (movementController == null)
        {
            movementController = gameObject.AddComponent<CharacterController>();
        }

        movementController.height = agent != null ? Mathf.Max(1f, agent.height * 0.9f) : 1.8f;
        movementController.radius = agent != null ? Mathf.Clamp(agent.radius * 0.7f, 0.2f, 0.4f) : 0.3f;
        movementController.center = Vector3.up * (movementController.height * 0.5f);
        movementController.stepOffset = Mathf.Min(0.3f, movementController.height * 0.2f);
        movementController.skinWidth = 0.08f;
        movementController.minMoveDistance = 0f;
    }

    private void IgnoreDoorGeometryWhenBaking()
    {
        for (int i = 0; i < animatedDoors.Length; i++)
        {
            opencloseDoor door = animatedDoors[i];
            if (door != null && door.openandclose != null && IsBossSizedDoor(door.transform))
            {
                MarkDoorAsDynamicGeometry(door.openandclose.gameObject);
            }
        }

        for (int i = 0; i < scriptedDoors.Length; i++)
        {
            if (scriptedDoors[i] != null && IsBossSizedDoor(scriptedDoors[i].transform))
            {
                MarkDoorAsDynamicGeometry(scriptedDoors[i].gameObject);
            }
        }
    }

    private void MarkDoorAsDynamicGeometry(GameObject doorObject)
    {
        NavMeshModifier modifier = doorObject.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = doorObject.AddComponent<NavMeshModifier>();
        }

        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;
    }

    private void CacheDoors()
    {
        EnsurePatrolThreeDoorController();
        animatedDoors = FindObjectsByType<opencloseDoor>(FindObjectsSortMode.None);
        scriptedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);

        for (int i = 0; i < animatedDoors.Length; i++)
        {
            opencloseDoor door = animatedDoors[i];
            if (door != null && door.openandclose == null)
            {
                door.openandclose = door.GetComponent<Animator>() ??
                                    door.GetComponentInChildren<Animator>(true);
            }

        }
    }

    private void EnsurePatrolThreeDoorController()
    {
        if (patrolPoints == null || patrolPoints.Length <= 2 || patrolPoints[2] == null)
        {
            return;
        }

        // Ada beberapa DoorFrame_Apt_01 di prefab; pilih instance bernama
        // persis DoorFrame_Apt_01 (1) yang paling dekat dengan PatrolPoint 3.
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        float bestDistance = float.MaxValue;
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.name != "DoorFrame_Apt_01 (1)")
            {
                continue;
            }

            float distance = HorizontalDistanceBetween(candidate.position, patrolPoints[2].position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                patrolThreeDoorRoot = candidate;
            }
        }

        if (patrolThreeDoorRoot == null)
        {
            Debug.LogWarning("BossController: DoorFrame_Apt_01 (1) untuk PatrolPoint 3 tidak ditemukan.", this);
            return;
        }

        opencloseDoor doorController = patrolThreeDoorRoot.GetComponent<opencloseDoor>();
        if (doorController == null)
        {
            doorController = patrolThreeDoorRoot.gameObject.AddComponent<opencloseDoor>();
        }

        if (doorController.openandclose == null)
        {
            doorController.openandclose = patrolThreeDoorRoot.GetComponent<Animator>() ??
                patrolThreeDoorRoot.GetComponentInChildren<Animator>(true);
        }

        if (doorController.openandclose == null)
        {
            Debug.LogWarning("BossController: Animator pintu DoorFrame_Apt_01 (1) tidak ditemukan.", patrolThreeDoorRoot);
        }
    }

    private static float HorizontalDistanceBetween(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private void TryOpenNearbyDoors()
    {
        bool isLeavingPointTwoForPointThree = currentState == BossState.Patrol &&
            patrolPointIndex == 2 && !isWaitingAtPatrolPoint;
        CloseBossOpenedDoorsAtPatrolPointTwo(isLeavingPointTwoForPointThree);

        bool isAtPointTwo = patrolPoints != null && patrolPoints.Length > 1 &&
            patrolPoints[1] != null &&
            HorizontalDistanceTo(patrolPoints[1].position) <= patrolArrivalDistance + 0.15f;

        // Point 2 harus tetap tertutup saat Boss tiba/menunggu. Saat jedanya
        // selesai, hanya pintu masuk ke Point 3 yang boleh terbuka.
        bool shouldKeepDoorsClosed = (isAtPointTwo && !isLeavingPointTwoForPointThree) ||
            (isWaitingAtPatrolPoint && currentState == BossState.Patrol);
        if (shouldKeepDoorsClosed)
        {
            CloseBossOpenedDoors(true);
            return;
        }

        if (isLeavingPointTwoForPointThree)
        {
            CloseBossOpenedDoorsExcept(patrolThreeDoorRoot);
        }
        else
        {
            CloseBossOpenedDoors(false);
        }

        for (int i = 0; i < animatedDoors.Length; i++)
        {
            opencloseDoor door = animatedDoors[i];
            if (door == null || !IsBossSizedDoor(door.transform))
            {
                continue;
            }

            if (currentState == BossState.Patrol && patrolPointIndex == 2 &&
                patrolThreeDoorRoot != null && door.transform != patrolThreeDoorRoot)
            {
                continue;
            }

            bool isPatrolThreeEntry = IsPatrolThreeDoorApproach(door.transform);
            if (IsDoorAtPatrolPointTwo(door.transform) && !isPatrolThreeEntry)
            {
                continue;
            }

            Vector3 doorCenter = GetDoorCenter(door.transform);
            if (!isPatrolThreeEntry &&
                !IsDoorAheadOrClose(doorCenter) && !IsDoorNearCurrentRoute(doorCenter))
            {
                continue;
            }

            if (!door.open)
            {
                door.SetOpen(true);
            }

            bossOpenedAnimatedDoors.Add(door);

            SetBossDoorCollisionIgnored(
                door.openandclose != null ? door.openandclose.transform : door.transform,
                true);

        }

        for (int i = 0; i < scriptedDoors.Length; i++)
        {
            Door door = scriptedDoors[i];
            if (door == null || !IsBossSizedDoor(door.transform))
            {
                continue;
            }

            if (IsDoorAtPatrolPointTwo(door.transform))
            {
                continue;
            }

            Vector3 doorCenter = GetDoorCenter(door.transform);
            if (!IsDoorAheadOrClose(doorCenter) && !IsDoorNearCurrentRoute(doorCenter))
            {
                continue;
            }

            if (!door.IsOpen)
            {
                door.SetOpen(true);
                bossOpenedScriptedDoors.Add(door);
            }

            SetBossDoorCollisionIgnored(door.transform, true);

        }
    }

    private void CloseBossOpenedDoors(bool closeAll)
    {
        float closeDistance = Mathf.Max(doorOpeningDistance + 1f, doorRouteLookAheadDistance + 1f);

        bossOpenedAnimatedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            float doorCloseDistance = door.transform == patrolThreeDoorRoot
                ? Mathf.Max(closeDistance, doorRouteLookAheadDistance + 2.5f)
                : closeDistance;
            if (!closeAll && Vector3.Distance(transform.position, GetDoorCenter(door.transform)) <= doorCloseDistance)
            {
                return false;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(
                door.openandclose != null ? door.openandclose.transform : door.transform,
                false);
            return true;
        });

        bossOpenedScriptedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            if (!closeAll && Vector3.Distance(transform.position, GetDoorCenter(door.transform)) <= closeDistance)
            {
                return false;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(door.transform, false);
            return true;
        });
    }

    private void CloseBossOpenedDoorsExcept(Transform allowedDoorRoot)
    {
        bossOpenedAnimatedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            if (allowedDoorRoot != null && door.transform == allowedDoorRoot)
            {
                return false;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(
                door.openandclose != null ? door.openandclose.transform : door.transform,
                false);
            return true;
        });

        bossOpenedScriptedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(door.transform, false);
            return true;
        });
    }

    private void CloseBossOpenedDoorsAtPatrolPointTwo(bool keepPatrolThreeEntryOpen)
    {
        bossOpenedAnimatedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            if (keepPatrolThreeEntryOpen && door.transform == patrolThreeDoorRoot)
            {
                return false;
            }

            if (!IsDoorAtPatrolPointTwo(door.transform))
            {
                return false;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(
                door.openandclose != null ? door.openandclose.transform : door.transform,
                false);
            return true;
        });

        bossOpenedScriptedDoors.RemoveWhere(door =>
        {
            if (door == null)
            {
                return true;
            }

            if (keepPatrolThreeEntryOpen && door.transform == patrolThreeDoorRoot)
            {
                return false;
            }

            if (!IsDoorAtPatrolPointTwo(door.transform))
            {
                return false;
            }

            door.SetOpen(false);
            SetBossDoorCollisionIgnored(door.transform, false);
            return true;
        });
    }

    private bool IsDoorAtPatrolPointTwo(Transform doorRoot)
    {
        if (doorRoot == null || patrolPoints == null || patrolPoints.Length <= 1 || patrolPoints[1] == null)
        {
            return false;
        }

        float exclusionRadius = Mathf.Max(2.5f, doorOpeningDistance + 0.3f);
        return HorizontalDistanceBetween(GetDoorCenter(doorRoot), patrolPoints[1].position) <= exclusionRadius;
    }

    private bool IsBossSizedDoor(Transform doorRoot)
    {
        if (doorRoot == null)
        {
            return false;
        }

        // Proyek juga memakai opencloseDoor pada pintu kabinet. Menganggapnya
        // sebagai pintu ruangan membuat animasi kabinet bergerak (termasuk Glass
        // sebagai child) ketika Boss lewat. Hanya pintu dengan collider setinggi
        // pintu manusia yang boleh dibuka atau diabaikan collision-nya oleh Boss.
        Collider[] colliders = doorRoot.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        Bounds combinedBounds = default;
        foreach (Collider doorCollider in colliders)
        {
            if (doorCollider == null || doorCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = doorCollider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(doorCollider.bounds);
            }
        }

        return hasBounds && combinedBounds.size.y >= 1.5f;
    }

    private bool IsDoorAheadOrClose(Vector3 doorPosition)
    {
        Vector3 toDoor = doorPosition - transform.position;
        toDoor.y = 0f;
        float distance = toDoor.magnitude;
        // Buka pintu sebelum Boss mencapai collider; pintu animasi butuh waktu
        // untuk berputar dan collider daun pintu masih aktif selama animasi.
        float openingLeadDistance = Mathf.Max(
            doorOpeningDistance,
            movementSpeed * 0.8f + (agent != null ? agent.radius : 0.35f) + 0.5f);
        if (distance > openingLeadDistance)
        {
            return false;
        }

        Vector3 travelDirection = GetTravelDirection();
        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            return distance <= 0.75f;
        }

        float alongPath = Vector3.Dot(toDoor, travelDirection);
        if (alongPath < -0.25f || alongPath > openingLeadDistance)
        {
            return false;
        }

        Vector3 offsetFromPath = toDoor - travelDirection * alongPath;
        return offsetFromPath.magnitude <= 1.25f;
    }

    private bool IsDoorNearCurrentRoute(Vector3 doorPosition)
    {
        Vector3 flatDoorPosition = doorPosition;
        flatDoorPosition.y = transform.position.y;

        if (CanUseAgent() && agent.hasPath && !agent.pathPending)
        {
            Vector3[] corners = agent.path.corners;
            float distanceAlongPath = 0f;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 segmentStart = corners[i];
                Vector3 segmentEnd = corners[i + 1];
                segmentStart.y = transform.position.y;
                segmentEnd.y = transform.position.y;

                Vector3 segment = segmentEnd - segmentStart;
                float segmentLength = segment.magnitude;
                if (segmentLength <= 0.001f)
                {
                    continue;
                }

                float alongSegment = Vector3.Dot(flatDoorPosition - segmentStart, segment / segmentLength);
                float clampedAlongSegment = Mathf.Clamp(alongSegment, 0f, segmentLength);
                Vector3 nearestPathPoint = segmentStart + segment / segmentLength * clampedAlongSegment;
                float lateralDistance = Vector3.Distance(flatDoorPosition, nearestPathPoint);
                float totalAlongPath = distanceAlongPath + clampedAlongSegment;

                if (totalAlongPath <= doorRouteLookAheadDistance && lateralDistance <= 1.8f)
                {
                    return true;
                }

                distanceAlongPath += segmentLength;
                if (distanceAlongPath > doorRouteLookAheadDistance)
                {
                    break;
                }
            }

            return false;
        }

        // Cadangan saat Boss bergerak dengan CharacterController karena
        // NavMesh gagal, memakai arah ke waypoint patrol berikutnya.
        Transform destination = currentState == BossState.Chase || currentState == BossState.Attack
            ? playerTarget
            : GetCurrentPatrolDestination();
        if (destination == null)
        {
            return false;
        }

        Vector3 route = destination.position - transform.position;
        route.y = 0f;
        float routeLength = route.magnitude;
        if (routeLength <= 0.1f)
        {
            return false;
        }

        Vector3 routeDirection = route / routeLength;
        Vector3 toDoor = flatDoorPosition - transform.position;
        toDoor.y = 0f;
        float alongRoute = Vector3.Dot(toDoor, routeDirection);
        if (alongRoute < -0.25f || alongRoute > Mathf.Min(routeLength, doorRouteLookAheadDistance))
        {
            return false;
        }

        Vector3 lateralOffset = toDoor - routeDirection * alongRoute;
        return lateralOffset.magnitude <= 1.8f;
    }

    private bool IsPatrolThreeDoorApproach(Transform doorRoot)
    {
        if (doorRoot == null || doorRoot != patrolThreeDoorRoot ||
            currentState != BossState.Patrol || patrolPointIndex != 2 || isWaitingAtPatrolPoint)
        {
            return false;
        }

        float openingDistance = Mathf.Max(doorRouteLookAheadDistance + 1f, 6f);
        return HorizontalDistanceTo(GetDoorCenter(doorRoot)) <= openingDistance;
    }

    private Vector3 GetDoorCenter(Transform doorRoot)
    {
        Collider[] colliders = doorRoot.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Collider doorCollider in colliders)
        {
            if (doorCollider == null || doorCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = doorCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(doorCollider.bounds);
            }
        }

        // Pivot pintu biasanya berada di engsel, bukan di tengah daun pintu.
        return hasBounds ? bounds.center : doorRoot.position;
    }

    private void SetBossDoorCollisionIgnored(Transform doorLeaf, bool ignored)
    {
        if (doorLeaf == null)
        {
            return;
        }

        Collider[] bossColliders = GetComponentsInChildren<Collider>(true);
        Collider[] doorColliders = doorLeaf.GetComponentsInChildren<Collider>(true);
        foreach (Collider bossCollider in bossColliders)
        {
            if (bossCollider == null || bossCollider.isTrigger)
            {
                continue;
            }

            foreach (Collider doorCollider in doorColliders)
            {
                if (doorCollider == null || doorCollider.isTrigger ||
                    bossCollider == doorCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(bossCollider, doorCollider, ignored);
            }
        }
    }

    private Vector3 GetTravelDirection()
    {
        if (CanUseAgent() && agent.hasPath && !agent.pathPending)
        {
            Vector3 direction = agent.steeringTarget - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        Transform destination = currentState == BossState.Chase
            ? playerTarget
            : GetCurrentPatrolDestination();
        if (destination == null)
        {
            return Vector3.zero;
        }

        Vector3 fallback = destination.position - transform.position;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.zero;
    }

    private void Update()
    {
        if (IsDead)
        {
            UpdateHeartbeatEffect();
            return;
        }

        if (playerTarget == null || agent == null)
        {
            UpdateHeartbeatEffect();
            ChangeState(BossState.Idle);
            UpdateIdleState();
            return;
        }

        float distanceToPlayer = Vector3.Distance(
            transform.position,
            playerTarget.position);

        TryOpenNearbyDoors();

        // Tunggu daun DoorFrame_Apt_01 (1) selesai membuka sebelum Boss
        // bergerak ke sisi lain. Gate ini berlaku untuk Patrol dan Chase,
        // supaya pergantian state tidak membuat Boss menerobos pintu.
        if (Time.time < patrolThreeDoorReadyAt)
        {
            SetWalkingAnimation(false);
            StopAgent();
            UpdateHeartbeatEffect();
            return;
        }

        // Deteksi Hero dilakukan setiap frame, termasuk saat Boss sedang
        // berjalan patrol atau menunggu di waypoint.
        bool canSeePlayer = CanSeePlayer();
        BossState nextState;

        if (canSeePlayer && distanceToPlayer <= chaseDistance)
        {
            nextState = DetermineState(distanceToPlayer, true);
        }
        else if ((currentState == BossState.Chase || currentState == BossState.Attack) &&
                 distanceToPlayer <= chaseDistance)
        {
            // Jangan langsung batalkan pengejaran hanya karena Hero sesaat
            // tertutup kusen/pintu. Tetap kejar selama masih dalam jangkauan.
            nextState = distanceToPlayer <= attackDistance
                ? BossState.Attack
                : BossState.Chase;
        }
        else if (patrolPoints != null && patrolPoints.Length > 0)
        {
            nextState = BossState.Patrol;
        }
        else
        {
            nextState = BossState.Idle;
        }

        if (nextState != currentState)
        {
            ChangeState(nextState);
        }

        switch (currentState)
        {
            case BossState.Idle:
                UpdateIdleState();
                break;

            case BossState.Patrol:
                UpdatePatrolState();
                break;

            case BossState.Chase:
                UpdateChaseState();
                break;

            case BossState.Attack:
                UpdateAttackState();
                break;
        }

        UpdateHeartbeatEffect();
    }

    private BossState DetermineState(float distanceToPlayer, bool canSeePlayer)
    {
        if (!canSeePlayer)
        {
            return BossState.Idle;
        }

        if (distanceToPlayer <= attackDistance)
        {
            return BossState.Attack;
        }

        if (distanceToPlayer <= chaseDistance)
        {
            return BossState.Chase;
        }

        return BossState.Idle;
    }

    private bool CanSeePlayer()
    {
        if (playerTarget == null)
        {
            return false;
        }

        Vector3 origin = eyePoint != null
            ? eyePoint.position
            : transform.position + Vector3.up * eyeHeight;

        Vector3 target = playerTarget.position + Vector3.up * 0.9f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return true;
        }

        Vector3 horizontalDirection = playerTarget.position - transform.position;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude > 0.001f &&
            Vector3.Angle(transform.forward, horizontalDirection) > scanFieldOfView * 0.5f)
        {
            return false;
        }

        direction /= distance;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (first, second) =>
            first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return hitTransform == playerTarget ||
                   hitTransform.IsChildOf(playerTarget);
        }

        return false;
    }

    private void ChangeState(BossState newState)
    {
        ExitState(currentState);

        currentState = newState;

        EnterState(currentState);
    }

    private void EnterState(BossState state)
    {
        switch (state)
        {
            case BossState.Idle:
                SetWalkingAnimation(false);
                StopAgent();
                break;

            case BossState.Chase:
                agent.stoppingDistance = stoppingDistance;
                SetWalkingAnimation(true);
                break;

            case BossState.Attack:
                SetWalkingAnimation(false);
                StopAgent();
                break;
        }
    }

    private void UpdatePatrolState()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetWalkingAnimation(false);
            StopAgent();
            return;
        }

        Transform destination = GetCurrentPatrolDestination();
        if (destination == null)
        {
            AdvancePatrolPoint();
            return;
        }

        if (isWaitingAtPatrolPoint)
        {
            SetWalkingAnimation(false);
            StopAgent();
            transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f, Space.World);

            if (Time.time >= patrolPauseEndsAt)
            {
                isWaitingAtPatrolPoint = false;
                isWaitingForPatrolStart = false;
                AdvancePatrolPoint();
            }

            return;
        }

        if (CanUseAgent())
        {
            agent.stoppingDistance = 0f;
        }

        MoveSafelyToward(destination.position, 0f);
        if (HasReachedPatrolDestination(destination.position))
        {
            BeginPatrolPause();
        }
    }

    private bool HasReachedPatrolDestination(Vector3 destination)
    {
        if (HorizontalDistanceTo(destination) <= patrolArrivalDistance)
        {
            return true;
        }

        if (navigationReady && CanUseAgent())
        {
            return !agent.pathPending &&
                   agent.pathStatus == NavMeshPathStatus.PathComplete &&
                   agent.hasPath &&
                   agent.remainingDistance <= patrolArrivalDistance + agent.radius;
        }

        return HorizontalDistanceTo(destination) <= patrolArrivalDistance;
    }

    private float HorizontalDistanceTo(Vector3 position)
    {
        return Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(position.x, position.z));
    }

    private void MoveSafelyToward(Vector3 destinationPosition, float stopDistance)
    {
        if (!navigationReady)
        {
            // Tetap bergerak bila runtime NavMesh gagal dibangun. Pintu pada
            // koridor patrol sudah dibuka oleh TryOpenNearbyDoors.
            MoveWithoutNavMesh(destinationPosition, stopDistance);
            return;
        }

        if (!CanUseAgent())
        {
            SetWalkingAnimation(false);
            if (!agent.enabled)
            {
                MoveWithoutNavMesh(destinationPosition, stopDistance);
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit recoveryHit, 1.5f, agent.areaMask) &&
                agent.Warp(recoveryHit.position))
            {
                transform.position = recoveryHit.position;
                agent.nextPosition = recoveryHit.position;
                return;
            }

            MoveWithoutNavMesh(destinationPosition, stopDistance);
            return;
        }

        MoveAlongCalculatedPath(destinationPosition, stopDistance);
    }

    private void MoveAlongCalculatedPath(Vector3 destinationPosition, float stopDistance)
    {
        if (!NavMesh.SamplePosition(destinationPosition, out NavMeshHit destinationHit, 2.5f, agent.areaMask))
        {
            if (currentState == BossState.Patrol)
            {
                // Waypoint dapat berada sedikit di luar permukaan hasil bake.
                // Tetap lanjutkan rute dengan collider fisik alih-alih
                // menghentikan Boss di waypoint sebelumnya.
                MoveOnPatrolFallback(destinationPosition, stopDistance);
                return;
            }

            SetWalkingAnimation(false);
            StopAgent();
            return;
        }

        Vector3 navigationDestination = destinationHit.position;
        agent.isStopped = false;
        bool destinationChanged = !hasNavigationDestination ||
            Vector3.Distance(lastNavigationDestination, navigationDestination) > 0.75f;

        // Jangan kirim ulang tujuan saat NavMeshAgent masih menghitung jalur.
        // Mengulang SetDestination selama pathPending dapat membuat pencarian
        // selalu dimulai ulang sehingga Boss tertahan di depan penghalang.
        bool retryMissingChasePath = currentState != BossState.Patrol &&
                                     !agent.pathPending && !agent.hasPath;
        if (destinationChanged || retryMissingChasePath)
        {
            agent.isStopped = false;
            if (!agent.SetDestination(navigationDestination))
            {
                if (currentState == BossState.Patrol)
                {
                    MoveOnPatrolFallback(destinationPosition, stopDistance);
                    return;
                }

                SetWalkingAnimation(false);
                return;
            }

            lastNavigationDestination = navigationDestination;
            hasNavigationDestination = true;
        }

        if (agent.pathPending)
        {
            SetWalkingAnimation(false);
            return;
        }

        // Jika permintaan pertama selesai tanpa menghasilkan path, jangan
        // mengirim permintaan identik setiap frame. Itu membuat Boss diam di
        // waypoint sebelumnya tanpa pernah mencapai fallback patrol.
        if (currentState == BossState.Patrol &&
            (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            MoveOnPatrolFallback(destinationPosition, stopDistance);
            return;
        }

        if (Vector3.Distance(transform.position, lastProgressPosition) > 0.08f)
        {
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }
        else if (Time.time - lastProgressTime >= stuckRepathDelay)
        {
            if (currentState == BossState.Patrol)
            {
                // PathComplete juga dapat macet di collider runtime (contohnya
                // pintu). Reset dan kirim ulang path setiap frame hanya
                // mengulang kegagalan yang sama, jadi lanjutkan patrol dengan
                // CharacterController setelah tidak ada progres.
                MoveOnPatrolFallback(destinationPosition, stopDistance);
                return;
            }

            agent.ResetPath();
            hasNavigationDestination = false;
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
            SetWalkingAnimation(false);
            return;
        }

        bool hasCompletePath = agent.pathStatus == NavMeshPathStatus.PathComplete;
        float remainingDistance = agent.remainingDistance;
        if (hasCompletePath && !float.IsInfinity(remainingDistance) &&
            remainingDistance <= stopDistance + 0.05f)
        {
            SetWalkingAnimation(false);
            return;
        }

        // Ikuti jalur yang benar-benar dihitung NavMesh. Saat jalurnya parsial,
        // steeringTarget adalah ujung jalur yang dapat dicapai (misalnya sisi
        // pintu), bukan titik patrol di balik dinding.
        Vector3 steeringDirection = agent.steeringTarget - transform.position;
        steeringDirection.y = 0f;
        if (steeringDirection.sqrMagnitude <= 0.0025f && hasCompletePath)
        {
            steeringDirection = navigationDestination - transform.position;
            steeringDirection.y = 0f;
        }

        if (steeringDirection.sqrMagnitude <= 0.0025f)
        {
            SetWalkingAnimation(false);
            return;
        }

        steeringDirection.Normalize();
        Quaternion facing = Quaternion.LookRotation(steeringDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            facing,
            rotationSpeed * Time.deltaTime);

        // Agent mengikuti jalur NavMesh dan OffMeshLink secara langsung,
        // termasuk ketika jalur sedang parsial dan pintu mulai dibuka.
        SetWalkingAnimation(agent.velocity.sqrMagnitude > 0.01f);
    }

    private void MoveOnPatrolFallback(Vector3 destination, float stopDistance)
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        if (movementController != null)
        {
            movementController.enabled = true;
        }

        hasNavigationDestination = false;
        lastProgressPosition = transform.position;
        lastProgressTime = Time.time;
        MoveWithoutNavMesh(destination, stopDistance);
    }

    private void MoveWithoutNavMesh(Vector3 destination, float stopDistance)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance <= stopDistance + 0.05f || distance <= 0.001f)
        {
            SetWalkingAnimation(false);
            return;
        }

        if (movementController != null && !movementController.enabled)
        {
            movementController.enabled = true;
        }

        direction /= distance;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            rotationSpeed * Time.deltaTime);

        float step = Mathf.Min(movementSpeed * Time.deltaTime, distance - stopDistance);
        Vector3 moved = MoveWithWallSlide(direction * step);
        SetWalkingAnimation(moved.sqrMagnitude > 0.0001f);
    }

    private Vector3 MoveWithWallSlide(Vector3 movement)
    {
        if (movementController == null || !movementController.enabled)
        {
            transform.position += movement;
            return movement;
        }

        Vector3 before = transform.position;
        movementController.Move(movement);
        return transform.position - before;
    }

    private void BeginPatrolPause()
    {
        RestoreNavigationAgentAtCurrentPosition();
        isWaitingAtPatrolPoint = true;
        // Titik pertama adalah BossRoom; titik 2, 3, dan 4 berhenti 5-7 detik.
        float pauseDuration = patrolPointIndex == 0
            ? initialCountdown
            : Random.Range(patrolPauseMinimum, patrolPauseMaximum);
        patrolPauseEndsAt = Time.time + pauseDuration;
        SetWalkingAnimation(false);
        StopAgent();
    }

    private void RestoreNavigationAgentAtCurrentPosition()
    {
        if (agent == null || agent.enabled ||
            !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, agent.areaMask))
        {
            return;
        }

        transform.position = hit.position;
        agent.enabled = true;
        if (agent.Warp(hit.position))
        {
            if (movementController != null)
            {
                movementController.enabled = false;
            }
        }
        else
        {
            agent.enabled = false;
        }
    }

    private Transform GetCurrentPatrolDestination()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return null;
        }

        patrolPointIndex = Mathf.Clamp(patrolPointIndex, 0, patrolPoints.Length - 1);
        return patrolPoints[patrolPointIndex];
    }

    private void AdvancePatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolPointIndex = 0;
            return;
        }

        patrolPointIndex = (patrolPointIndex + 1) % patrolPoints.Length;
    }

    private void ExitState(BossState state)
    {
        if (state == BossState.Chase)
        {
            SetWalkingAnimation(false);
        }

        if (state == BossState.Attack)
        {
            if (pendingAttack != null)
            {
                StopCoroutine(pendingAttack);
                pendingAttack = null;
            }

            if (HasValidAnimator())
            {
                anim.CrossFade(IdleState, 0.08f, 0);
            }
        }
    }

    private void UpdateIdleState()
    {
        SetWalkingAnimation(false);
        StopAgent();
    }

    private void UpdateChaseState()
    {
        if (CanUseAgent())
        {
            agent.stoppingDistance = stoppingDistance;
        }

        MoveSafelyToward(playerTarget.position, stoppingDistance);
    }

    private void UpdateAttackState()
    {
        SetWalkingAnimation(false);
        StopAgent();
        RotateTowardsPlayer();

        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (pendingAttack != null)
        {
            return;
        }
        nextAttackTime = Time.time + attackCooldown;
        pendingAttack = StartCoroutine(PlayAttackAndDamage());
    }

    private System.Collections.IEnumerator PlayAttackAndDamage()
    {
        if (HasValidAnimator())
        {
            anim.CrossFade(AttackState, 0.08f, 0);
        }

        // Damage masuk setelah gerakan serangan terlihat.
        yield return new WaitForSeconds(0.35f);

        if (!IsDead && currentState == BossState.Attack && playerTarget != null &&
            CanSeePlayer() &&
            Vector3.Distance(transform.position, playerTarget.position) <= attackDistance + 0.35f)
        {
            DamagePlayer();
        }

        yield return new WaitForSeconds(0.55f);
        if (HasValidAnimator() && !IsDead && currentState == BossState.Attack)
        {
            anim.CrossFade(IdleState, 0.1f, 0);
        }

        pendingAttack = null;
    }

    private void DamagePlayer()
    {
        if (playerTarget == null || attackDamage <= 0)
        {
            return;
        }

        PlayerHealth playerHealth = playerTarget.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private void StopAgent()
    {
        if (!CanUseAgent())
        {
            hasNavigationDestination = false;
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        hasNavigationDestination = false;
    }

    private bool CanUseAgent()
    {
        return agent != null &&
               agent.isActiveAndEnabled &&
               agent.isOnNavMesh;
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (HasValidAnimator())
        {
            anim.SetBool(IsWalkingParameter, isWalking);
        }
    }

    private bool HasValidAnimator()
    {
        return anim != null && anim.runtimeAnimatorController != null;
    }

    private void RotateTowardsPlayer()
    {
        if (playerTarget == null)
        {
            return;
        }

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            10f * Time.deltaTime);
    }

    public void TakeDamage(int damage)
    {
        Debug.Log("Boss tidak dapat mati dan mengabaikan damage.", this);
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        SetWalkingAnimation(false);
        StopAgent();

        if (HasValidAnimator())
        {
            anim.CrossFade("m_death_A", 0.08f, 0);
        }

        Debug.Log("Boss mati.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        chaseDistance = Mathf.Max(0f, chaseDistance);
        attackDistance = Mathf.Max(0f, attackDistance);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
        attackDamage = Mathf.Max(0, attackDamage);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        movementSpeed = Mathf.Max(0f, movementSpeed);
        movementAcceleration = Mathf.Max(0f, movementAcceleration);
        navigationRadius = Mathf.Clamp(navigationRadius, 0.2f, 0.5f);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        patrolPauseMinimum = Mathf.Max(0f, patrolPauseMinimum);
        patrolPauseMaximum = Mathf.Max(patrolPauseMinimum, patrolPauseMaximum);
        initialCountdown = Mathf.Max(0f, initialCountdown);
        lookAroundSpeed = Mathf.Max(0f, lookAroundSpeed);
        patrolArrivalDistance = Mathf.Max(0.05f, patrolArrivalDistance);
        scanFieldOfView = Mathf.Clamp(scanFieldOfView, 1f, 360f);
        doorOpeningDistance = Mathf.Max(0.25f, doorOpeningDistance);
        doorRouteLookAheadDistance = Mathf.Max(1f, doorRouteLookAheadDistance);
        heartbeatEffectDistance = Mathf.Max(1f, heartbeatEffectDistance);
        heartbeatEffectMaxAlpha = Mathf.Clamp(heartbeatEffectMaxAlpha, 0f, 0.5f);
        stuckRepathDelay = Mathf.Max(0.25f, stuckRepathDelay);
    }
#endif

    private void CreateTimerUI()
    {
        if (!createTimerUI)
        {
            return;
        }

        GameObject canvasObject = new GameObject("BossTimerCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("BossTimerPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);
        panelRect.sizeDelta = new Vector2(500f, 64f);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.04f, 0.04f, 0.82f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("BossTimerText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        bossTimerText = textObject.GetComponent<Text>();
        bossTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bossTimerText.fontSize = 20;
        bossTimerText.fontStyle = FontStyle.Bold;
        bossTimerText.color = Color.white;
        bossTimerText.alignment = TextAnchor.MiddleLeft;
        bossTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bossTimerText.verticalOverflow = VerticalWrapMode.Truncate;
        bossTimerText.raycastTarget = false;
    }

    private void CreateHeartbeatOverlay()
    {
        GameObject canvasObject = new GameObject(
            "BossHeartbeatOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject = new GameObject(
            "RedHeartbeatPulse",
            typeof(RectTransform),
            typeof(Image));
        overlayObject.transform.SetParent(canvasObject.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        heartbeatOverlay = overlayObject.GetComponent<Image>();
        heartbeatOverlay.color = new Color(1f, 0f, 0f, 0f);
        heartbeatOverlay.raycastTarget = false;
        heartbeatOverlay.enabled = false;
    }

    private void UpdateHeartbeatEffect()
    {
        if (heartbeatOverlay == null)
        {
            return;
        }

        bool isThreatening = !IsDead && playerTarget != null &&
            (currentState == BossState.Chase || currentState == BossState.Attack);
        float distance = isThreatening
            ? Vector3.Distance(transform.position, playerTarget.position)
            : float.MaxValue;
        bool showEffect = isThreatening && distance <= heartbeatEffectDistance;

        heartbeatOverlay.enabled = showEffect;
        if (!showEffect)
        {
            return;
        }

        // Dua kilatan pendek memberi kesan detak jantung yang berulang.
        float beatTime = Time.unscaledTime % 1.1f;
        float firstBeat = Mathf.Exp(-Mathf.Pow((beatTime - 0.12f) / 0.075f, 2f));
        float secondBeat = 0.7f * Mathf.Exp(-Mathf.Pow((beatTime - 0.32f) / 0.085f, 2f));
        float pulse = Mathf.Max(firstBeat, secondBeat);
        float proximity = 1f - Mathf.Clamp01(distance / heartbeatEffectDistance);
        float alpha = heartbeatEffectMaxAlpha * proximity * pulse;
        heartbeatOverlay.color = new Color(1f, 0f, 0f, alpha);
    }

    private void LateUpdate()
    {
        if (bossTimerText == null)
        {
            return;
        }

        string timerLabel;
        if (currentState == BossState.Attack)
        {
            timerLabel = "Boss menyerang player";
        }
        else if (currentState == BossState.Chase)
        {
            timerLabel = "Boss mengejar player";
        }
        else if (isWaitingForPatrolStart && Time.time < patrolStartAt)
        {
            timerLabel = "Boss di BossRoom - pindah dalam " +
                Mathf.CeilToInt(patrolStartAt - Time.time) + " detik";
        }
        else if (isWaitingAtPatrolPoint && Time.time < patrolPauseEndsAt && patrolPointIndex == 0)
        {
            timerLabel = "Boss di BossRoom - pindah dalam " +
                Mathf.CeilToInt(patrolPauseEndsAt - Time.time) + " detik";
        }
        else if (isWaitingAtPatrolPoint && Time.time < patrolPauseEndsAt)
        {
            timerLabel = "Boss mengawasi - pindah dalam " +
                Mathf.CeilToInt(patrolPauseEndsAt - Time.time) + " detik";
        }
        else
        {
            Transform nextPoint = GetCurrentPatrolDestination();
            timerLabel = nextPoint != null
                ? "Boss bergerak ke: " + nextPoint.name
                : "Boss sedang patroli";
        }

        bossTimerText.text = timerLabel;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        if (playerTarget != null)
        {
            Vector3 origin = eyePoint != null
                ? eyePoint.position
                : transform.position + Vector3.up * eyeHeight;

            Gizmos.color = CanSeePlayer() ? Color.green : Color.gray;
            Gizmos.DrawLine(origin, playerTarget.position + Vector3.up * 0.9f);
        }
    }
}

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.AI.Navigation;
using SojaExiles;

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
    [SerializeField, Min(0f)] private float initialCountdown = 20f;

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

    [Tooltip("Kecepatan putar NavMeshAgent Boss.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField] private bool createTimerUI = true;
    [SerializeField, Min(0.25f)] private float doorOpeningDistance = 2.2f;
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
    private opencloseDoor[] animatedDoors = System.Array.Empty<opencloseDoor>();
    private Door[] scriptedDoors = System.Array.Empty<Door>();
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
        agent.stoppingDistance = stoppingDistance;
        // Agent menjadi sumber jalur, sedangkan CharacterController
        // menggerakkan Transform Boss.
        agent.updatePosition = false;
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
            runtimeNavigationSurface.collectObjects = CollectObjects.All;
            runtimeNavigationSurface.layerMask = Physics.DefaultRaycastLayers;
            runtimeNavigationSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            runtimeNavigationSurface.ignoreNavMeshAgent = true;
            runtimeNavigationSurface.ignoreNavMeshObstacle = true;
        }

        if (runtimeNavigationSurface.navMeshData == null)
        {
            runtimeNavigationSurface.BuildNavMesh();
        }

        navigationReady = runtimeNavigationSurface.navMeshData != null &&
                          NavMesh.SamplePosition(
                              transform.position,
                              out _,
                              3f,
                              agent.areaMask);

        if (!navigationReady)
        {
            Debug.LogWarning(
                "BossController: jalur NavMesh tidak dapat dibangun di scene ini. " +
                "Periksa apakah lantai dan dinding memiliki mesh yang dapat dipakai untuk navigasi.",
                this);
            return;
        }

        // Jalur lengkap digerakkan oleh NavMeshAgent supaya Boss mengikuti
        // koridor tanpa tertahan collider daun pintu.
        agent.updatePosition = false;
        agent.updateRotation = false;
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

    private void TryOpenNearbyDoors()
    {
        for (int i = 0; i < animatedDoors.Length; i++)
        {
            opencloseDoor door = animatedDoors[i];
            if (door == null || !IsBossSizedDoor(door.transform))
            {
                continue;
            }

            if (!IsDoorAheadOrClose(door.transform.position))
            {
                continue;
            }

            if (!door.open)
            {
                door.SetOpen(true);
            }

        }

        for (int i = 0; i < scriptedDoors.Length; i++)
        {
            Door door = scriptedDoors[i];
            if (door == null || !IsBossSizedDoor(door.transform))
            {
                continue;
            }

            if (!IsDoorAheadOrClose(door.transform.position))
            {
                continue;
            }

            if (!door.IsOpen)
            {
                door.SetOpen(true);
            }

        }
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
            return;
        }

        if (playerTarget == null || agent == null)
        {
            ChangeState(BossState.Idle);
            UpdateIdleState();
            return;
        }

        float distanceToPlayer = Vector3.Distance(
            transform.position,
            playerTarget.position);

        TryOpenNearbyDoors();

        // Boss hanya mendeteksi player saat sedang berhenti memindai area.
        // Saat berjalan antar titik, ia tetap mengikuti rute patrol.
        bool isScanning = isWaitingAtPatrolPoint && Time.time < patrolPauseEndsAt;
        bool canSeePlayer = isScanning && CanSeePlayer();
        BossState nextState;

        if (isScanning && canSeePlayer && distanceToPlayer <= chaseDistance)
        {
            nextState = DetermineState(distanceToPlayer, true);
        }
        else if (isScanning)
        {
            nextState = BossState.Patrol;
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
        if (navigationReady && CanUseAgent())
        {
            MoveAlongCalculatedPath(destinationPosition, stopDistance);
            return;
        }

        Vector3 toDestination = destinationPosition - transform.position;
        toDestination.y = 0f;
        float distance = toDestination.magnitude;
        if (distance <= stopDistance || distance <= 0.0001f)
        {
            SetWalkingAnimation(false);
            return;
        }

        Vector3 direction = toDestination / distance;
        Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, rotationSpeed * Time.deltaTime);

        float step = Mathf.Min(movementSpeed * Time.deltaTime, distance - stopDistance);
        Vector3 moved = MoveWithWallSlide(direction * step);
        SetWalkingAnimation(moved.sqrMagnitude > 0.0001f);
    }

    private void MoveAlongCalculatedPath(Vector3 destinationPosition, float stopDistance)
    {
        if (!NavMesh.SamplePosition(destinationPosition, out NavMeshHit destinationHit, 2.5f, agent.areaMask))
        {
            SetWalkingAnimation(false);
            StopAgent();
            return;
        }

        Vector3 navigationDestination = destinationHit.position;
        bool destinationChanged = !hasNavigationDestination ||
            Vector3.Distance(lastNavigationDestination, navigationDestination) > 0.75f;

        // Jangan kirim ulang tujuan saat NavMeshAgent masih menghitung jalur.
        // Mengulang SetDestination selama pathPending dapat membuat pencarian
        // selalu dimulai ulang sehingga Boss tertahan di depan penghalang.
        if (destinationChanged || (!agent.pathPending && !agent.hasPath))
        {
            agent.isStopped = false;
            if (!agent.SetDestination(navigationDestination))
            {
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

        if (Vector3.Distance(transform.position, lastProgressPosition) > 0.08f)
        {
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }
        else if (Time.time - lastProgressTime >= stuckRepathDelay)
        {
            agent.ResetPath();
            hasNavigationDestination = false;
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
            SetWalkingAnimation(false);
            return;
        }

        bool hasCompletePath = agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (agent.updatePosition != hasCompletePath)
        {
            // Hindari mencampur gerakan fisik CharacterController dengan Agent.
            agent.nextPosition = transform.position;
            agent.updatePosition = hasCompletePath;
        }

        float remainingDistance = agent.remainingDistance;
        if (hasCompletePath && !float.IsInfinity(remainingDistance) &&
            remainingDistance <= stopDistance + 0.05f)
        {
            SetWalkingAnimation(false);
            return;
        }

        // Jalur parsial sering berakhir di sisi dalam pintu yang belum bisa
        // dimasuki NavMesh. Tetap bergerak ke tujuan sebenarnya; CharacterController
        // menahan Boss pada dinding dan TryOpenNearbyDoors membuka pintu di depannya.
        Vector3 steeringDirection = hasCompletePath
            ? agent.steeringTarget - transform.position
            : navigationDestination - transform.position;
        steeringDirection.y = 0f;
        if (steeringDirection.sqrMagnitude <= 0.0025f)
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

        if (hasCompletePath)
        {
            // Agent menggerakkan Transform tepat di sepanjang jalur NavMesh.
            SetWalkingAnimation(agent.velocity.sqrMagnitude > 0.01f);
            return;
        }

        float step = movementSpeed * Time.deltaTime;
        Vector3 moved = MoveWithWallSlide(steeringDirection * step);
        agent.nextPosition = transform.position;
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
        isWaitingAtPatrolPoint = true;
        // Titik pertama adalah BossRoom: Boss berhenti 20 detik saat mulai
        // dan setiap kali kembali dari dapur. Titik lainnya memakai jeda 5-7 detik.
        float pauseDuration = patrolPointIndex == 0
            ? initialCountdown
            : Random.Range(patrolPauseMinimum, patrolPauseMaximum);
        patrolPauseEndsAt = Time.time + pauseDuration;
        SetWalkingAnimation(false);
        StopAgent();
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
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        patrolPauseMinimum = Mathf.Max(0f, patrolPauseMinimum);
        patrolPauseMaximum = Mathf.Max(patrolPauseMinimum, patrolPauseMaximum);
        initialCountdown = Mathf.Max(0f, initialCountdown);
        lookAroundSpeed = Mathf.Max(0f, lookAroundSpeed);
        patrolArrivalDistance = Mathf.Max(0.05f, patrolArrivalDistance);
        scanFieldOfView = Mathf.Clamp(scanFieldOfView, 1f, 360f);
        doorOpeningDistance = Mathf.Max(0.25f, doorOpeningDistance);
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

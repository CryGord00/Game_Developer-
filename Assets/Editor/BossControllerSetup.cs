using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BossControllerSetup
{
    [MenuItem("Tools/Boss/Setup Selected Boss")]
    private static void SetupSelectedBoss()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog(
                "Boss Setup",
                "Pilih GameObject Boss terlebih dahulu di Hierarchy.",
                "OK");
            return;
        }

        MonoBehaviour boss = selectedObject.GetComponent("BossController") as MonoBehaviour;

        if (boss == null)
        {
            EditorUtility.DisplayDialog(
                "Boss Setup",
                "GameObject yang dipilih tidak memiliki komponen BossController.",
                "OK");
            return;
        }

        NavMeshAgent agent = selectedObject.GetComponent<NavMeshAgent>();
        Animator animator = FindAnimator(selectedObject);
        Transform player = FindPlayer();
        Transform eyePoint = FindOrCreateEyePoint(selectedObject.transform);
        Transform spawnPoint = FindSpawnPoint();

        SerializedObject serializedBoss = new SerializedObject(boss);
        serializedBoss.FindProperty("playerTarget").objectReferenceValue = player;
        serializedBoss.FindProperty("agent").objectReferenceValue = agent;
        serializedBoss.FindProperty("anim").objectReferenceValue = animator;
        serializedBoss.FindProperty("eyePoint").objectReferenceValue = eyePoint;
        serializedBoss.FindProperty("bossSpawnPoint").objectReferenceValue = spawnPoint;
        serializedBoss.FindProperty("chaseDistance").floatValue = 10f;
        serializedBoss.FindProperty("attackDistance").floatValue = 2f;
        serializedBoss.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(selectedObject.scene);
        Selection.activeGameObject = selectedObject;

        string playerMessage = player == null
            ? "Player Target belum ditemukan. Isi manual pada Inspector."
            : "Player Target berhasil diisi.";

        string spawnMessage = spawnPoint == null
            ? "Boss Spawn Point belum ditemukan. Isi manual pada Inspector."
            : "Boss Spawn Point berhasil diisi.";

        EditorUtility.DisplayDialog(
            "Boss Setup Berhasil",
            "EyePoint dibuat dan referensi Boss sudah diatur.\n\n" + playerMessage +
            "\n" + spawnMessage +
            "\n\nPastikan NavMesh sudah di-bake dan Animator Controller sudah diisi.",
            "OK");
    }

    [MenuItem("Tools/Boss/Setup Selected Boss", true)]
    private static bool ValidateSetupSelectedBoss()
    {
        return Selection.activeGameObject != null &&
               Selection.activeGameObject.GetComponent("BossController") != null;
    }

    private static Animator FindAnimator(GameObject bossObject)
    {
        Animator animator = bossObject.GetComponent<Animator>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            return animator;
        }

        Animator[] childAnimators = bossObject.GetComponentsInChildren<Animator>(true);

        foreach (Animator childAnimator in childAnimators)
        {
            if (childAnimator.runtimeAnimatorController != null)
            {
                return childAnimator;
            }
        }

        return animator != null ? animator : bossObject.GetComponentInChildren<Animator>(true);
    }

    private static Transform FindPlayer()
    {
        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                return playerObject.transform;
            }
        }
        catch (UnityException)
        {
            // Tag Player belum dibuat; pencarian berdasarkan nama tetap dilakukan.
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

        foreach (Transform currentTransform in transforms)
        {
            if (currentTransform.name.Equals("hero", System.StringComparison.OrdinalIgnoreCase) ||
                currentTransform.name.Equals("player", System.StringComparison.OrdinalIgnoreCase))
            {
                return currentTransform;
            }
        }

        return null;
    }

    private static Transform FindSpawnPoint()
    {
        GameObject spawnObject = GameObject.Find("BossSpawnPoint");
        return spawnObject != null ? spawnObject.transform : null;
    }

    private static Transform FindOrCreateEyePoint(Transform bossTransform)
    {
        Transform eyePoint = bossTransform.Find("EyePoint");

        if (eyePoint == null)
        {
            GameObject eyeObject = new GameObject("EyePoint");
            eyePoint = eyeObject.transform;
            eyePoint.SetParent(bossTransform, false);
            eyePoint.localPosition = new Vector3(0f, 1.5f, 0f);
            Undo.RegisterCreatedObjectUndo(eyeObject, "Create Boss EyePoint");
        }

        return eyePoint;
    }
}

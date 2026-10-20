using System.IO;
using UnityEditor;
using UnityEngine;

// Taruh di folder Assets/Editor.
// Cara pakai: pilih prefab/model 3D (di Project atau Hierarchy),
// lalu klik menu Tools > Inventory > Generate Icon From Selected Object.
// Hasilnya: Assets/Icons/<nama>_Icon.png (sudah berupa Sprite, background transparan).
public static class IconGenerator
{
    const int Size = 256;

    [MenuItem("Tools/Inventory/Generate Icon From Selected Object")]
    public static void Generate()
    {
        GameObject source = Selection.activeGameObject;
        if (source == null)
        {
            EditorUtility.DisplayDialog("Generate Icon",
                "Pilih dulu model/prefab kunci (di Project atau Hierarchy).", "OK");
            return;
        }

        // Salin objek ke posisi jauh supaya tidak tercampur dengan isi scene
        GameObject instance = Object.Instantiate(source);
        instance.name = source.name + "_IconTemp";
        instance.SetActive(true);
        instance.transform.position = new Vector3(5000f, 5000f, 5000f);
        instance.transform.rotation = Quaternion.identity;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(instance);
            EditorUtility.DisplayDialog("Generate Icon",
                "Objek yang dipilih tidak punya model 3D (Renderer).", "OK");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        float radius = bounds.extents.magnitude;

        // Kamera sementara dengan background transparan
        GameObject camGO = new GameObject("IconCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = radius * 1.05f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = radius * 10f;

        Vector3 direction = new Vector3(0.6f, 0.5f, -1f).normalized;   // sudut pandang miring
        camGO.transform.position = bounds.center + direction * (radius * 4f);
        camGO.transform.LookAt(bounds.center);

        RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;

        // Simpan sebagai PNG
        if (!AssetDatabase.IsValidFolder("Assets/Icons"))
            AssetDatabase.CreateFolder("Assets", "Icons");

        string assetPath = "Assets/Icons/" + source.name + "_Icon.png";
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());

        // Bersihkan objek sementara
        cam.targetTexture = null;
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(instance);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        // Jadikan Sprite
        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        Object sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        Selection.activeObject = sprite;
        EditorGUIUtility.PingObject(sprite);
        Debug.Log("Icon dibuat: " + assetPath);
    }
}
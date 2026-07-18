using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 透過PNGのImport設定とタイトルUIへの割り当てを同期する。
/// </summary>
public static class TitleLogoUIEditorUtility
{
    private const string PrototypeScenePath = "Assets/Scenes/GamePrototype.unity";
    private const string LogoAssetPath = "Assets/UI/Logo/FakeOrderLogo.png";

    [MenuItem("Fake Order/Title Logo/Apply Transparent PNG")]
    public static void ApplyInCurrentScene()
    {
        ConfigureImporter();

        GameObject legacyLogo = GameObject.Find("TitleLogo3D");
        if (legacyLogo != null)
            Object.DestroyImmediate(legacyLogo);

        RoleSelectionUI roleSelectionUI = Object.FindAnyObjectByType<RoleSelectionUI>(FindObjectsInactive.Include);
        if (roleSelectionUI == null)
            throw new MissingReferenceException("RoleSelectionUI was not found in the current scene.");

        Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoAssetPath);
        if (logoSprite == null)
            throw new MissingReferenceException($"Logo sprite was not imported: {LogoAssetPath}");

        SerializedObject serializedUI = new SerializedObject(roleSelectionUI);
        serializedUI.FindProperty("titleLogoSprite").objectReferenceValue = logoSprite;
        serializedUI.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(roleSelectionUI);

        Canvas canvas = roleSelectionUI.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            EditorUtility.SetDirty(canvas);
        }

        foreach (Canvas sceneCanvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (sceneCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;
            sceneCanvas.pixelPerfect = true;
            EditorUtility.SetDirty(sceneCanvas);
        }

        CanvasScaler scaler = roleSelectionUI.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        EditorSceneManager.MarkSceneDirty(roleSelectionUI.gameObject.scene);
        Selection.activeGameObject = roleSelectionUI.gameObject;
    }

    public static void ApplySaveForCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        ApplyInCurrentScene();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Transparent title logo applied to GamePrototype scene.");
    }

    private static void ConfigureImporter()
    {
        AssetDatabase.ImportAsset(LogoAssetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(LogoAssetPath) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException($"TextureImporter was not found: {LogoAssetPath}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}

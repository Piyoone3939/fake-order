using UnityEngine;

/// <summary>
/// GamePrototype シーンのセットアップスクリプト
/// 編集モードで一度だけシーン内オブジェクトを生成し、以後は配置済みオブジェクトを使用する
/// </summary>
[ExecuteAlways]
public class GamePrototypeSetup : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool sceneObjectsGenerated;
    private bool isGenerating;

    private void Awake()
    {
        // 既に編集モードで配置済みなら、Play開始時には何も作り直さない。
        if (Application.isPlaying && !sceneObjectsGenerated)
            BakeSceneObjects();

    }

#if UNITY_EDITOR
    private void Update()
    {
        // 旧シーンを開いた最初の1回だけ、編集可能な通常オブジェクトとして配置する。
        if (!Application.isPlaying && !sceneObjectsGenerated)
            BakeSceneObjects();
        else if (!Application.isPlaying)
        {
            EnsureRoleSelectionObjects();
        }
    }
#endif

    [ContextMenu("Bake Prototype Scene Objects")]
    public void BakeSceneObjects()
    {
        if (sceneObjectsGenerated || isGenerating)
            return;

        isGenerating = true;

        try
        {
            RemoveTemplateCameraAndAudioListeners();
            SetupScene();
            sceneObjectsGenerated = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
        finally
        {
            isGenerating = false;
        }
    }

    private void RemoveTemplateCameraAndAudioListeners()
    {
        // シーンテンプレート由来の既存カメラを削除（新規カメラとの競合防止）
        // 壁と同じ位置に埋まっており、Depthの都合で後続カメラの描画を妨げるため
        Camera[] existingCameras = FindObjectsByType<Camera>();
        foreach (Camera cam in existingCameras)
        {
            DestroySetupObject(cam.gameObject);
        }

        // 既存の AudioListener を削除（重複防止）
        AudioListener[] existingListeners = FindObjectsByType<AudioListener>();
        foreach (AudioListener listener in existingListeners)
        {
            DestroySetupObject(listener);
        }
    }

    public void SetupScene()
    {
        Debug.Log("🔧 Setting up GamePrototype scene...");
        
        // 1. GameManager をシーンに配置
        CreateGameManager();
        
        // 2. Spy ビュー（FPS視点）をセットアップ
        CreateSpyView();
        
        // 3. Organizer ビュー（俯瞰マップ）をセットアップ
        CreateOrganizerView();

        // 4. ロール選択画面（タイトルロゴを含む）
        CreateRoleSelectionObjects();
        
        // 6. 基本施設オブジェクト
        CreateFacilityLayout();
        
        // 7. テスト用情報端末を配置
        CreateTestTerminals();
        
        // 8. 脱出ポイント
        CreateExitPoint();
        
        Debug.Log("✅ GamePrototype scene setup complete!");
    }
    
    private void CreateGameManager()
    {
        GameObject gameManagerGO = new GameObject("GameManager");
        gameManagerGO.AddComponent<GameManager>();
        gameManagerGO.AddComponent<GameState>();
        gameManagerGO.AddComponent<TimerManager>();
        gameManagerGO.AddComponent<GameEventSystem>();
    }

    private void CreateRoleSelectionObjects()
    {
        GameObject roleUIGO = new GameObject("RoleSelectionUI");
        Canvas roleCanvas = roleUIGO.AddComponent<Canvas>();
        roleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        roleCanvas.sortingOrder = 100;
        roleCanvas.pixelPerfect = true;

        var scaler = roleUIGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        roleUIGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        roleUIGO.AddComponent<RoleSelectionUI>();

        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

#if UNITY_EDITOR
    private void EnsureRoleSelectionObjects()
    {
        if (FindAnyObjectByType<RoleSelectionUI>() != null)
            return;

        CreateRoleSelectionObjects();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    
    private void CreateSpyView()
    {
        // Spy オブジェクト
        GameObject spyGO = new GameObject("Spy");
        spyGO.transform.position = new Vector3(0, 1, 0);
        
        // CharacterController
        var charController = spyGO.AddComponent<CharacterController>();
        charController.height = 1.8f;
        charController.radius = 0.3f;
        
        // SpyController
        var spyController = spyGO.AddComponent<SpyController>();
        
        // カメラ
        GameObject cameraGO = new GameObject("Camera");
        cameraGO.transform.parent = spyGO.transform;
        cameraGO.transform.localPosition = new Vector3(0, 0.6f, 0);
        
        var camera = cameraGO.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        cameraGO.tag = "MainCamera";

        // Audio Listener（Spy側のみに1つ）
        cameraGO.AddComponent<AudioListener>();
        
        // SpyUI Canvas
        GameObject spyUIGO = new GameObject("SpyUI");
        var spyUICanvas = spyUIGO.AddComponent<Canvas>();
        spyUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        spyUICanvas.pixelPerfect = true;
        var spyUIScaler = spyUIGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        spyUIScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        spyUIScaler.referenceResolution = new Vector2(1920, 1080);
        spyUIGO.AddComponent<SpyUI>();
        
        // Suspicion Gauge をSpyに追加
        spyGO.AddComponent<SuspicionGauge>();
    }
    
    private void CreateOrganizerView()
    {
        // Organizer オブジェクト
        GameObject organizerGO = new GameObject("Organizer");
        organizerGO.transform.position = new Vector3(10, 10, 0);

        // OrganizerController
        var organizerController = organizerGO.AddComponent<OrganizerController>();
        var delayedSurveillance = organizerGO.AddComponent<DelayedSurveillance>();
        organizerGO.AddComponent<InformationFreshness>();

        // 俯瞰カメラ
        // organizerGOはワールド座標(10,10,0)にあるため、単純に子として原点(ローカル0,0,0)に置くと
        // 施設(X/Z: -10〜10、原点中心)から外れた位置を見下ろすことになり、テクスチャの大半が
        // 何も映らない空間になってしまう。ワールド座標で明示的に施設の中心上空へ配置する。
        GameObject mapCameraGO = new GameObject("MapCamera");
        mapCameraGO.transform.parent = organizerGO.transform;
        mapCameraGO.transform.position = new Vector3(0f, 15f, 0f);

        var mapCamera = mapCameraGO.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = 11f; // 施設(20x20)に対して余白1程度でぴったり収まるサイズ
        mapCamera.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Spyのメインカメラと画面を取り合わないよう、俯瞰カメラはRenderTextureにのみ描画する
        var mapRenderTexture = new RenderTexture(1024, 1024, 16);
        mapCamera.targetTexture = mapRenderTexture;
        organizerController.ConfigureMapCamera(mapCamera, mapRenderTexture);

        // 遅延監視カメラ：現状1部屋しかない施設内に仮想的なエリアを重ねて配置する
        // (Terminal_3(5,0.5,-5)はどの円にも入らないため、意図的に「盲点エリア」として扱われる)
        CreateSurveillanceCamera(delayedSurveillance, "執務エリア（一般執務室相当）",
            delayTime: 10f, center: new Vector3(-2.5f, 0f, 2.5f), radius: 6f, orthoSize: 8f);
        CreateSurveillanceCamera(delayedSurveillance, "外周・搬入口エリア",
            delayTime: 45f, center: new Vector3(-8f, 0f, -8f), radius: 5f, orthoSize: 6f);
        CreateSurveillanceCamera(delayedSurveillance, "重要区画（金庫室）",
            delayTime: 0f, center: new Vector3(8f, 0f, 8f), radius: 3f, orthoSize: 4f);

        // OrganizerUI Canvas
        GameObject organizerUIGO = new GameObject("OrganizerUI");
        var organizerUICanvas = organizerUIGO.AddComponent<Canvas>();
        organizerUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        organizerUICanvas.pixelPerfect = true;
        var organizerUIScaler = organizerUIGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        organizerUIScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        organizerUIScaler.referenceResolution = new Vector2(1920, 1080);
        organizerUIGO.AddComponent<OrganizerUI>();
        organizerUIGO.AddComponent<CommandLog>();

        // Audio Listener は削除（Spy側のみに1つ）
        // organizerUIGO.AddComponent<AudioListener>();
    }

    private void CreateSurveillanceCamera(DelayedSurveillance system, string areaName,
        float delayTime, Vector3 center, float radius, float orthoSize)
    {
        GameObject camGO = new GameObject($"SurveillanceCamera_{areaName}");
        camGO.transform.position = new Vector3(center.x, 15f, center.z);
        camGO.transform.rotation = Quaternion.Euler(90, 0, 0);

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;

        // mapCameraと同様、RenderTextureにのみ描画させ、Unityの通常フレーム更新に任せる
        var liveRenderTexture = new RenderTexture(256, 256, 16);
        cam.targetTexture = liveRenderTexture;

        system.AddCameraArea(new DelayedSurveillance.CameraArea
        {
            areaName = areaName,
            delayTime = delayTime,
            surveillanceCamera = cam,
            liveRenderTexture = liveRenderTexture,
            areaCenter = center,
            areaRadius = radius
        });
    }
    
    private void CreateFacilityLayout()
    {
        // 床
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.transform.localScale = new Vector3(20, 1, 20);
        
        // MeshCollider を保持（Plane は MeshCollider が付く）
        DestroySetupObject(floor.GetComponent<Rigidbody>());
        
        // 壁（簡易版）
        CreateWall("WallFront", new Vector3(0, 2, -10), new Vector3(20, 4, 0.5f));
        CreateWall("WallBack", new Vector3(0, 2, 10), new Vector3(20, 4, 0.5f));
        CreateWall("WallLeft", new Vector3(-10, 2, 0), new Vector3(0.5f, 4, 20));
        CreateWall("WallRight", new Vector3(10, 2, 0), new Vector3(0.5f, 4, 20));
    }
    
    private void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        DestroySetupObject(wall.GetComponent<BoxCollider>());
        DestroySetupObject(wall.GetComponent<Rigidbody>());
        wall.AddComponent<BoxCollider>();
    }
    
    private void CreateTestTerminals()
    {
        // テスト用に3つの情報端末を配置
        CreateTerminal(1, new Vector3(-5, 0.5f, 0));
        CreateTerminal(2, new Vector3(0, 0.5f, 5));
        CreateTerminal(3, new Vector3(5, 0.5f, -5));
    }
    
    private void CreateTerminal(int id, Vector3 position)
    {
        GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        terminal.name = string.Format("Terminal_{0}", id);
        terminal.transform.position = position;
        terminal.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        DestroySetupObject(terminal.GetComponent<BoxCollider>());
        DestroySetupObject(terminal.GetComponent<Rigidbody>());
        
        // 当たり判定は見た目のメッシュより大きめに取り、視点の高さ(約1.6m)から
        // 水平に見てもRaycastが当たるようにする（見た目の1m角メッシュのままだと目線より低く外れる）
        var collider = terminal.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, 0.25f, 0);
        collider.size = new Vector3(2.5f, 2.5f, 2.5f);

        var terminalScript = terminal.AddComponent<Terminal>();
        terminalScript.SetTerminalId(id);
    }
    
    private void CreateExitPoint()
    {
        GameObject exitPoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        exitPoint.name = "ExitPoint";
        exitPoint.transform.position = new Vector3(-8, 0.5f, -8);
        exitPoint.transform.localScale = new Vector3(1, 0.5f, 1);
        DestroySetupObject(exitPoint.GetComponent<CapsuleCollider>());
        DestroySetupObject(exitPoint.GetComponent<Rigidbody>());
        
        // Terminalと同様、視点の高さから水平に見てもRaycastが当たるよう当たり判定を広げる
        var collider = exitPoint.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 0.5f, 0);
        collider.height = 4f;
        collider.radius = 2f;

        exitPoint.AddComponent<ExitPoint>();
    }

    private static void DestroySetupObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}

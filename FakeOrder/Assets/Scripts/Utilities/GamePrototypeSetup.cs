using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// GamePrototype シーンのセットアップスクリプト
/// 編集モードで一度だけシーン内オブジェクトを生成し、以後は配置済みオブジェクトを使用する
/// </summary>
[ExecuteAlways]
public class GamePrototypeSetup : MonoBehaviour
{
    private const float FloorHeight = 5f;
    private const float FacilityPlanScale = 1.4f;
    private const float FacilityWidth = 61.6f;
    private const float FacilityDepth = 42f;
    // 俯瞰マップ専用。TagManagerに名前を追加しなくてもレイヤー番号は描画制御に使用できる。
    private const int FirstFloorMapLayer = 30;
    private static readonly Vector3 SpySpawnPosition = new Vector3(0f, 1.05f, -11.5f);
    private enum RoomDoorSide { North, South, East, West }
    [SerializeField, HideInInspector] private bool sceneObjectsGenerated;
    private bool isGenerating;
    private Transform facilityRoot;
    private Material officeDeskMaterial;
    private Material officeMonitorMaterial;
    private Material officeInteractiveMonitorMaterial;
    private Material officeChairMaterial;
    private Material officePotMaterial;
    private Material officePlantMaterial;
    private Material employeeNpcMaterial;
    private Material guardNpcMaterial;
    private Material npcSkinMaterial;

    private void Awake()
    {
        // 既に編集モードで配置済みなら、Play開始時には何も作り直さない。
        if (Application.isPlaying && !sceneObjectsGenerated)
            BakeSceneObjects();
        else if (Application.isPlaying)
        {
            GameObject room = GameObject.Find("OrganizerManagementRoom");
            if (room == null || room.transform.Find("ManagementRoom_Interactive_v3") == null ||
                GameObject.Find("ManagementRoomCamera") == null)
                CreateOrganizerManagementRoom();
        }

    }

#if UNITY_EDITOR
    private void Update()
    {
        // 旧シーンを開いた最初の1回だけ、編集可能な通常オブジェクトとして配置する。
        if (!Application.isPlaying && !sceneObjectsGenerated)
            BakeSceneObjects();
        else if (!Application.isPlaying)
        {
            EnsureDisplayRenderingCamera();
            EnsureRoleSelectionObjects();
            EnsureOrganizerManagementRoomObjects();
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

        // タイトル／ロール選択中もDisplay 1へ最低1台は直接描画し、
        // Gameビューの「No cameras rendering」表示を出さない。
        EnsureDisplayRenderingCamera();

        // 4. ロール選択画面（タイトルロゴを含む）
        CreateRoleSelectionObjects();
        
        // 6. 基本施設オブジェクト
        CreateFacilityLayout();
        
        // 7. テスト用情報端末を配置
        CreateTestTerminals();
        
        // 8. 脱出ポイント
        CreateExitPoint();

        // 9. 室内・通路監視カラネットワーク
        CreateSurveillanceNetwork();
        
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

    public Camera EnsureDisplayRenderingCamera()
    {
        GameObject cameraObject = GameObject.Find("DisplayBackdropCamera");
        if (cameraObject == null)
            cameraObject = new GameObject("DisplayBackdropCamera");

        Camera displayCamera = cameraObject.GetComponent<Camera>();
        if (displayCamera == null)
            displayCamera = cameraObject.AddComponent<Camera>();

        displayCamera.enabled = true;
        displayCamera.targetTexture = null;
        displayCamera.targetDisplay = 0;
        displayCamera.depth = -100f;
        displayCamera.cullingMask = 0;
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f);
        displayCamera.allowHDR = false;
        displayCamera.allowMSAA = false;
        displayCamera.useOcclusionCulling = false;
        return displayCamera;
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

    private void EnsureOrganizerManagementRoomObjects()
    {
        GameObject room = GameObject.Find("OrganizerManagementRoom");
        if (room != null && room.transform.Find("ManagementRoom_Interactive_v3") != null &&
            GameObject.Find("ManagementRoomCamera") != null)
            return;

        CreateOrganizerManagementRoom();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
    
    private void CreateSpyView()
    {
        // Spy オブジェクト
        GameObject spyGO = new GameObject("Spy");
        spyGO.transform.position = SpySpawnPosition;
        
        // CharacterController
        var charController = spyGO.AddComponent<CharacterController>();
        charController.height = 1.8f;
        charController.radius = 0.3f;
        
        // SpyController
        var spyController = spyGO.AddComponent<SpyController>();
        spyGO.AddComponent<SpyDisguiseController>();
        spyGO.AddComponent<SpyBotController>();
        
        // カメラ
        GameObject cameraGO = new GameObject("Camera");
        cameraGO.transform.parent = spyGO.transform;
        cameraGO.transform.localPosition = new Vector3(0, 0.6f, 0);
        
        var camera = cameraGO.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.cullingMask &= ~(1 << SpyDisguiseController.DisguiseVisualLayer);
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
        var organizerCharacter = organizerGO.AddComponent<CharacterController>();
        organizerCharacter.height = 1.8f;
        organizerCharacter.radius = 0.3f;
        organizerGO.AddComponent<DelayedSurveillance>();
        organizerGO.AddComponent<InformationFreshness>();

        // 俯瞰カメラ
        // organizerGOはワールド座標(10,10,0)にあるため、単純に子として原点(ローカル0,0,0)に置くと
        // 施設(X/Z: -10〜10、原点中心)から外れた位置を見下ろすことになり、テクスチャの大半が
        // 何も映らない空間になってしまう。ワールド座標で明示的に施設の中心上空へ配置する。
        GameObject mapCameraGO = new GameObject("MapCamera");
        // 管理ルームを歩くOrganizerとは独立させ、移動してもマップの位置を変えない。
        // 1階のみ表示し、2・3階を俳瞰で透視できない高さに置く。
        mapCameraGO.transform.position = new Vector3(0f, 18f, 0f);

        var mapCamera = mapCameraGO.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = 31f;
        mapCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
        mapCamera.cullingMask = 1 << FirstFloorMapLayer;

        // Spyのメインカメラと画面を取り合わないよう、俯瞰カメラはRenderTextureにのみ描画する
        var mapRenderTexture = new RenderTexture(1024, 1024, 16);
        mapCamera.targetTexture = mapRenderTexture;
        organizerController.ConfigureMapCamera(mapCamera, mapRenderTexture);

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

        CreateOrganizerManagementRoom(organizerController);

        // Audio Listener は削除（Spy側のみに1つ）
        // organizerUIGO.AddComponent<AudioListener>();
    }

    private void CreateOrganizerManagementRoom(OrganizerController organizerController = null)
    {
        organizerController ??= FindAnyObjectByType<OrganizerController>(FindObjectsInactive.Include);
        if (organizerController == null)
            return;

        GameObject existingRoom = GameObject.Find("OrganizerManagementRoom");
        if (existingRoom != null)
            DestroySetupObject(existingRoom);
        GameObject existingCamera = GameObject.Find("ManagementRoomCamera");
        if (existingCamera != null)
            DestroySetupObject(existingCamera);

        GameObject mapCameraObject = GameObject.Find("MapCamera");
        if (mapCameraObject != null)
            mapCameraObject.transform.SetParent(null, true);

        CharacterController organizerCharacter = organizerController.GetComponent<CharacterController>();
        if (organizerCharacter == null)
            organizerCharacter = organizerController.gameObject.AddComponent<CharacterController>();
        organizerCharacter.height = 1.8f;
        organizerCharacter.radius = 0.3f;
        organizerCharacter.center = Vector3.zero;
        organizerController.transform.position = new Vector3(56f, 0.9f, -5.2f);
        organizerController.transform.rotation = Quaternion.identity;

        const float roomX = 56f;
        GameObject root = new GameObject("OrganizerManagementRoom");
        Transform room = root.transform;

        Material wallMaterial = CreateManagementRoomMaterial("ManagementRoom_White", new Color(0.78f, 0.84f, 0.90f));
        Material floorMaterial = CreateManagementRoomMaterial("ManagementRoom_Floor", new Color(0.28f, 0.36f, 0.46f));
        Material frameMaterial = CreateManagementRoomMaterial("ManagementRoom_Navy", new Color(0.025f, 0.075f, 0.13f));
        Material screenMaterial = CreateManagementRoomMaterial("ManagementRoom_Screen", new Color(0.015f, 0.20f, 0.38f), true);
        Material accentMaterial = CreateManagementRoomMaterial("ManagementRoom_Blue", new Color(0.02f, 0.48f, 1f), true);

        GameObject marker = new GameObject("ManagementRoom_Interactive_v3");
        marker.transform.SetParent(room, false);

        GameObject RoomBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = CreateBlock(name, position, scale, room);
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return block;
        }

        // 明るい白基調の管理室。カメラから机、端末、壁面モニター、保安扉が見える構成。
        RoomBlock("ManagementRoom_Floor", new Vector3(roomX, -0.15f, 0f), new Vector3(18f, 0.3f, 15f), floorMaterial);
        RoomBlock("ManagementRoom_Ceiling", new Vector3(roomX, 5.35f, 0f), new Vector3(18f, 0.3f, 15f), wallMaterial);
        RoomBlock("ManagementRoom_BackWall", new Vector3(roomX, 2.6f, 7.4f), new Vector3(18f, 5.5f, 0.3f), wallMaterial);
        RoomBlock("ManagementRoom_LeftWall", new Vector3(roomX - 8.85f, 2.6f, 0f), new Vector3(0.3f, 5.5f, 15f), wallMaterial);
        RoomBlock("ManagementRoom_RightWall", new Vector3(roomX + 8.85f, 2.6f, 0f), new Vector3(0.3f, 5.5f, 15f), wallMaterial);

        RoomBlock("ManagementDesk", new Vector3(roomX, 0.55f, -0.5f), new Vector3(8f, 1.1f, 2.2f), wallMaterial);
        RoomBlock("DeskMonitor", new Vector3(roomX, 1.85f, 0.05f), new Vector3(4.4f, 2.35f, 0.25f), frameMaterial);
        RoomBlock("DeskMonitorScreen", new Vector3(roomX, 1.85f, -0.09f),
            new Vector3(4.05f, 2.0f, 0.05f), screenMaterial);
        RoomBlock("DeskKeyboard", new Vector3(roomX, 1.2f, -1.7f), new Vector3(3.2f, 0.15f, 0.85f), frameMaterial);
        RoomBlock("OperatorChair", new Vector3(roomX + 4.8f, 1.0f, -2.6f), new Vector3(1.7f, 2f, 1.7f), frameMaterial);

        RoomBlock("WallDisplayFrame", new Vector3(roomX + 2.1f, 3.25f, 7.05f), new Vector3(10.8f, 3.4f, 0.32f), frameMaterial);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                RoomBlock($"WallCameraTile_{row + 1}_{column + 1}",
                    new Vector3(roomX - 1.15f + column * 3.15f, 3.95f - row * 1.35f, 6.82f),
                    new Vector3(2.7f, 1.05f, 0.12f), screenMaterial);
            }
        }

        RoomBlock("SecureDoor", new Vector3(roomX - 6.65f, 1.65f, 7.05f), new Vector3(3.1f, 3.3f, 0.45f), frameMaterial);
        RoomBlock("DoorAccessPanel", new Vector3(roomX - 4.65f, 1.55f, 6.75f), new Vector3(0.45f, 0.8f, 0.18f), accentMaterial);

        // 後続フェーズで別の仕事を割り当てられる設備。現時点では編集可能な配置の土台とする。
        RoomBlock("CommandPlanningTable", new Vector3(roomX - 5.4f, 0.65f, -2.5f), new Vector3(3.4f, 1.3f, 2.2f), wallMaterial);
        RoomBlock("CommandPlanningDisplay", new Vector3(roomX - 5.4f, 1.38f, -2.5f), new Vector3(2.8f, 0.12f, 1.6f), screenMaterial);
        RoomBlock("CommunicationsConsole", new Vector3(roomX + 6.4f, 1.1f, 2.5f), new Vector3(2.2f, 2.2f, 1.1f), frameMaterial);
        RoomBlock("CommunicationsScreen", new Vector3(roomX + 5.82f, 1.45f, 2.5f), new Vector3(0.08f, 1.25f, 1.55f), accentMaterial);
        RoomBlock("EvidenceCabinet", new Vector3(roomX - 7.6f, 1.3f, 3.4f), new Vector3(1.5f, 2.6f, 3.3f), frameMaterial);
        RoomBlock("EmergencyControlPanel", new Vector3(roomX + 8.55f, 1.65f, -1.8f), new Vector3(0.18f, 1.4f, 1.4f), accentMaterial);

        Vector3[] lightPositions =
        {
            new Vector3(roomX - 5f, 4.8f, -2f),
            new Vector3(roomX, 4.8f, 2f),
            new Vector3(roomX + 5f, 4.8f, -2f)
        };
        for (int i = 0; i < lightPositions.Length; i++)
        {
            GameObject lightObject = new GameObject($"ManagementRoomLight_{i + 1}");
            lightObject.transform.SetParent(room, false);
            lightObject.transform.position = lightPositions[i];
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 2.0f;
            light.color = new Color(0.70f, 0.86f, 1f);
            light.shadows = LightShadows.None;
        }

        GameObject cameraObject = new GameObject("ManagementRoomCamera");
        cameraObject.transform.SetParent(organizerController.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        Camera managementCamera = cameraObject.AddComponent<Camera>();
        managementCamera.fieldOfView = 68f;
        managementCamera.nearClipPlane = 0.1f;
        managementCamera.farClipPlane = 30f;
        managementCamera.clearFlags = CameraClearFlags.SolidColor;
        managementCamera.backgroundColor = new Color(0.04f, 0.08f, 0.13f);

        AudioListener organizerListener = cameraObject.AddComponent<AudioListener>();
        organizerListener.enabled = false;
        organizerController.ConfigureManagementRoomCamera(managementCamera, null);
    }

    private static Material CreateManagementRoomMaterial(string materialName, Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        return material;
    }

    [ContextMenu("Rebuild Organizer Management Room")]
    public void RebuildOrganizerManagementRoom()
    {
        CreateOrganizerManagementRoom();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void CreateSurveillanceCamera(DelayedSurveillance system, Transform parent, string areaName,
        float delayTime, Vector3 position, Vector3 lookAt, Vector3 areaCenter, float radius)
    {
        position = ScaleFacilityPosition(position);
        lookAt = ScaleFacilityPosition(lookAt);
        areaCenter = ScaleFacilityPosition(areaCenter);
        radius *= FacilityPlanScale;

        GameObject camGO = new GameObject($"SurveillanceCamera_{areaName}");
        camGO.transform.SetParent(parent, false);
        camGO.transform.position = position;
        camGO.transform.LookAt(lookAt);

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 72f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 65f;

        var liveRenderTexture = new RenderTexture(480, 270, 16);
        cam.targetTexture = liveRenderTexture;

        system.AddCameraArea(new DelayedSurveillance.CameraArea
        {
            areaName = areaName,
            delayTime = delayTime,
            surveillanceCamera = cam,
            liveRenderTexture = liveRenderTexture,
            areaCenter = areaCenter,
            areaRadius = radius
        });
    }
    
    private void CreateFacilityLayout()
    {
        CreateOfficeMaterials();
        EnsureSpyEmployeeDisguise();
        GameObject root = new GameObject("Facility_3Floor");
        facilityRoot = root.transform;

        Transform floor1 = CreateFloorShell(1, "GENERAL_FLOOR", 0f);
        Transform floor2 = CreateFloorShell(2, "IMPORTANT_DATA_FLOOR", FloorHeight);
        Transform floor3 = CreateFloorShell(3, "TOP_SECURITY_FLOOR", FloorHeight * 2f);

        CreateFirstFloorLayout(floor1, 0f);
        CreateSecondFloorLayout(floor2, FloorHeight);
        CreateThirdFloorLayout(floor3, FloorHeight * 2f);
        ConfigureFloorNavigation(floor1);
        ConfigureFloorNavigation(floor2);
        ConfigureFloorNavigation(floor3);
        GameObject firstFloorNpcPopulation = CreateFirstFloorNpcPopulation();
        CreateSecondFloorNpcPopulation();
        CreateThirdFloorNpcPopulation();

        CreateBlock("Roof", new Vector3(0f, FloorHeight * 3f - 0.15f, 0f),
            new Vector3(FacilityWidth, 0.3f, FacilityDepth), facilityRoot);
        CreateElevatorPanels();

        // MapCameraはこの階層だけを描画する。Spy/監視カメラは全レイヤー描画なので通常どおり見える。
        SetLayerRecursively(floor1.gameObject, FirstFloorMapLayer);
        SetLayerRecursively(firstFloorNpcPopulation, FirstFloorMapLayer);
    }

    private void CreateOfficeMaterials()
    {
        officeDeskMaterial = CreateManagementRoomMaterial("Office_Wood", new Color(0.52f, 0.36f, 0.22f));
        officeMonitorMaterial = CreateManagementRoomMaterial("Office_Monitor", new Color(0.035f, 0.055f, 0.075f));
        officeInteractiveMonitorMaterial = CreateManagementRoomMaterial("Office_InteractiveMonitor", new Color(0.02f, 0.12f, 0.2f), true);
        officeChairMaterial = CreateManagementRoomMaterial("Office_Chair", new Color(0.08f, 0.11f, 0.14f));
        officePotMaterial = CreateManagementRoomMaterial("Office_Pot", new Color(0.22f, 0.18f, 0.14f));
        officePlantMaterial = CreateManagementRoomMaterial("Office_Plant", new Color(0.12f, 0.34f, 0.18f));
        employeeNpcMaterial = CreateManagementRoomMaterial("NPC_Employee", new Color(0.68f, 0.73f, 0.78f));
        guardNpcMaterial = CreateManagementRoomMaterial("NPC_Guard", new Color(0.08f, 0.24f, 0.42f));
        npcSkinMaterial = CreateManagementRoomMaterial("NPC_Skin", new Color(0.74f, 0.55f, 0.42f));
    }

    private void ConfigureFloorNavigation(Transform floor)
    {
        NavMeshSurface surface = floor.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = floor.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
    }

    public void BuildFacilityNavMeshes()
    {
        string[] floorNames = { "Floor_1_GENERAL_FLOOR", "Floor_2_IMPORTANT_DATA_FLOOR", "Floor_3_TOP_SECURITY_FLOOR" };
        foreach (string floorName in floorNames)
        {
            GameObject floor = GameObject.Find(floorName);
            NavMeshSurface surface = floor != null ? floor.GetComponent<NavMeshSurface>() : null;
            if (surface == null)
                throw new MissingComponentException($"NavMeshSurface was not generated: {floorName}");
            surface.RemoveData();
            surface.BuildNavMesh();
        }
    }

    private GameObject CreateFirstFloorNpcPopulation()
    {
        GameObject population = new GameObject("NpcPopulation_1F");
        population.transform.SetParent(facilityRoot, false);

        CreateOfficeNpc(population.transform, "NPC_Employee_01", OfficeNpcController.NpcRole.Employee,
            new Vector3(-20f, 0f, -8f), new[]
            {
                new Vector3(-20f, 0f, -8f), new Vector3(-4f, 0f, -8f),
                new Vector3(-4f, 0f, 5f), new Vector3(-20f, 0f, 5f)
            });
        CreateOfficeNpc(population.transform, "NPC_Employee_02", OfficeNpcController.NpcRole.Employee,
            new Vector3(-18f, 0f, -5f), new[]
            {
                new Vector3(-18f, 0f, -5f), new Vector3(-8f, 0f, -5f),
                new Vector3(-8f, 0f, 3f), new Vector3(-18f, 0f, 3f)
            });
        CreateOfficeNpc(population.transform, "NPC_Employee_03", OfficeNpcController.NpcRole.Employee,
            new Vector3(-14f, 0f, 8.5f), new[]
            {
                new Vector3(-14f, 0f, 8.5f), new Vector3(-11.5f, 0f, 6.2f),
                new Vector3(-5f, 0f, 5.5f), new Vector3(-6f, 0f, -6f)
            });
        CreateOfficeNpc(population.transform, "NPC_Employee_04", OfficeNpcController.NpcRole.Employee,
            new Vector3(17f, 0f, 6f), new[]
            {
                new Vector3(17f, 0f, 6f), new Vector3(14f, 0f, 4f),
                new Vector3(7f, 0f, 5.5f), new Vector3(5f, 0f, -4f)
            });
        CreateOfficeNpc(population.transform, "NPC_Employee_05", OfficeNpcController.NpcRole.Employee,
            new Vector3(2f, 0f, -7f), new[]
            {
                new Vector3(2f, 0f, -7f), new Vector3(4f, 0f, -7f),
                new Vector3(4f, 0f, -9f), new Vector3(2f, 0f, -9f)
            });
        CreateOfficeNpc(population.transform, "NPC_Employee_06", OfficeNpcController.NpcRole.Employee,
            new Vector3(16f, 0f, -7f), new[]
            {
                new Vector3(16f, 0f, -7f), new Vector3(12f, 0f, -4.5f),
                new Vector3(8f, 0f, -8f), new Vector3(2f, 0f, -8f)
            });

        CreateOfficeNpc(population.transform, "NPC_Guard_01", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(0f, 0f, -11f), new[]
            {
                new Vector3(0f, 0f, -12f), new Vector3(-4f, 0f, -8f),
                new Vector3(-4f, 0f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(-8f, 0f, -8f)
            });
        CreateOfficeNpc(population.transform, "NPC_Guard_02", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(8f, 0f, -9f), new[]
            {
                new Vector3(8f, 0f, -9f), new Vector3(12f, 0f, -5f),
                new Vector3(9f, 0f, -4f), new Vector3(5f, 0f, -5f), new Vector3(4f, 0f, -8f)
            });

        return population;
    }

    private GameObject CreateSecondFloorNpcPopulation()
    {
        GameObject population = new GameObject("NpcPopulation_2F");
        population.transform.SetParent(facilityRoot, false);
        const float y = FloorHeight;

        CreateOfficeNpc(population.transform, "NPC_2F_Employee_01", OfficeNpcController.NpcRole.Employee,
            new Vector3(-5f, y, -12f), RectangleRoute(-5f, -1f, -12f, -6f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Employee_02", OfficeNpcController.NpcRole.Employee,
            new Vector3(1f, y, -12f), RectangleRoute(1f, 5f, -12f, -6f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Employee_03", OfficeNpcController.NpcRole.Employee,
            new Vector3(-5f, y, -5f), RectangleRoute(-5f, -2f, -5f, -2f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Employee_04", OfficeNpcController.NpcRole.Employee,
            new Vector3(2f, y, -10f), RectangleRoute(2f, 5f, -11f, -8f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Employee_05", OfficeNpcController.NpcRole.Employee,
            new Vector3(-18f, y, 8f), RectangleRoute(-18f, -12f, 8f, 11f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Employee_06", OfficeNpcController.NpcRole.Employee,
            new Vector3(-18f, y, -9f), RectangleRoute(-18f, -12f, -9f, -6f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Guard_01", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(0f, y, -13f), RectangleRoute(-5f, 5f, -13f, -8f, y));
        CreateOfficeNpc(population.transform, "NPC_2F_Guard_02", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(5f, y, -6f), RectangleRoute(2f, 6f, -9f, -3f, y));
        return population;
    }

    private GameObject CreateThirdFloorNpcPopulation()
    {
        GameObject population = new GameObject("NpcPopulation_3F");
        population.transform.SetParent(facilityRoot, false);
        const float y = FloorHeight * 2f;

        CreateOfficeNpc(population.transform, "NPC_3F_Employee_01", OfficeNpcController.NpcRole.Employee,
            new Vector3(-5f, y, -11f), RectangleRoute(-5f, -1f, -11f, -6f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Employee_02", OfficeNpcController.NpcRole.Employee,
            new Vector3(1f, y, -11f), RectangleRoute(1f, 5f, -11f, -6f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Employee_03", OfficeNpcController.NpcRole.Employee,
            new Vector3(-5f, y, -5f), RectangleRoute(-5f, -2f, -5f, -2f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Employee_04", OfficeNpcController.NpcRole.Employee,
            new Vector3(2f, y, -5f), RectangleRoute(2f, 5f, -5f, -2f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Guard_01", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(0f, y, -13f), RectangleRoute(-5f, 5f, -13f, -9f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Guard_02", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(-5f, y, -6f), RectangleRoute(-6f, -2f, -9f, -3f, y));
        CreateOfficeNpc(population.transform, "NPC_3F_Guard_03", OfficeNpcController.NpcRole.SecurityGuard,
            new Vector3(5f, y, -6f), RectangleRoute(2f, 6f, -9f, -3f, y));
        return population;
    }

    private static Vector3[] RectangleRoute(float minX, float maxX, float minZ, float maxZ, float y)
    {
        return new[]
        {
            new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ),
            new Vector3(maxX, y, maxZ), new Vector3(minX, y, maxZ)
        };
    }

    private void CreateOfficeNpc(Transform parent, string objectName, OfficeNpcController.NpcRole role,
        Vector3 spawnPosition, Vector3[] route)
    {
        GameObject npc = new GameObject(objectName);
        npc.transform.SetParent(parent, false);
        npc.transform.position = ScaleFacilityPosition(spawnPosition);

        var agent = npc.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

        var collider = npc.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.9f, 0f);
        collider.height = 1.8f;
        collider.radius = 0.34f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = objectName + "_Body";
        body.transform.SetParent(npc.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
        DestroySetupObject(body.GetComponent<Collider>());
        SetObjectMaterial(body, role == OfficeNpcController.NpcRole.SecurityGuard ? guardNpcMaterial : employeeNpcMaterial);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = objectName + "_Head";
        head.transform.SetParent(npc.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        head.transform.localScale = Vector3.one * 0.42f;
        DestroySetupObject(head.GetComponent<Collider>());
        SetObjectMaterial(head, npcSkinMaterial);

        if (role == OfficeNpcController.NpcRole.SecurityGuard)
        {
            GameObject cap = CreateLocalBlock(objectName + "_SecurityCap", new Vector3(0f, 1.88f, 0.02f),
                new Vector3(0.55f, 0.12f, 0.58f), npc.transform);
            DestroySetupObject(cap.GetComponent<Collider>());
            SetObjectMaterial(cap, guardNpcMaterial);
        }

        Vector3[] scaledRoute = new Vector3[route.Length];
        for (int i = 0; i < route.Length; i++)
            scaledRoute[i] = ScaleFacilityPosition(route[i]);

        int assignedFloor = Mathf.Clamp(Mathf.RoundToInt(spawnPosition.y / FloorHeight) + 1, 1, 3);
        OfficeNpcController controller = npc.AddComponent<OfficeNpcController>();
        controller.Configure(role, objectName, assignedFloor, scaledRoute);
        controller.ConfigureAppearance(GetStableAppearanceVariant(objectName));
    }

    private static int GetStableAppearanceVariant(string value)
    {
        int total = 0;
        for (int i = 0; i < value.Length; i++)
            total = (total * 31 + value[i]) & 0x7fffffff;
        return total % OfficeNpcController.EmployeeAppearanceVariantCount;
    }

    private void EnsureSpyEmployeeDisguise()
    {
        SpyController spy = FindAnyObjectByType<SpyController>(FindObjectsInactive.Include);
        if (spy == null)
            return;

        SpyDisguiseController disguise = spy.GetComponent<SpyDisguiseController>();
        if (disguise == null)
            disguise = spy.gameObject.AddComponent<SpyDisguiseController>();
        if (spy.GetComponent<SpyBotController>() == null)
            spy.gameObject.AddComponent<SpyBotController>();

        Transform visual = spy.transform.Find("SpyDisguiseVisual");
        if (visual == null)
        {
            var visualObject = new GameObject("SpyDisguiseVisual");
            visualObject.transform.SetParent(spy.transform, false);
            visual = visualObject.transform;
        }

        Renderer bodyRenderer = visual.Find("EmployeeBody")?.GetComponent<Renderer>();
        if (bodyRenderer == null)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "EmployeeBody";
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
            DestroySetupObject(body.GetComponent<Collider>());
            bodyRenderer = body.GetComponent<Renderer>();
        }

        Renderer headRenderer = visual.Find("EmployeeHead")?.GetComponent<Renderer>();
        if (headRenderer == null)
        {
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "EmployeeHead";
            head.transform.SetParent(visual, false);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            head.transform.localScale = Vector3.one * 0.42f;
            DestroySetupObject(head.GetComponent<Collider>());
            headRenderer = head.GetComponent<Renderer>();
        }

        bodyRenderer.sharedMaterial = employeeNpcMaterial;
        headRenderer.sharedMaterial = npcSkinMaterial;
        SetLayerRecursively(visual.gameObject, SpyDisguiseController.DisguiseVisualLayer);
        disguise.Configure(bodyRenderer, headRenderer);

        Camera spyCamera = spy.GetComponentInChildren<Camera>(true);
        if (spyCamera != null)
            spyCamera.cullingMask &= ~(1 << SpyDisguiseController.DisguiseVisualLayer);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static Vector3 ScaleFacilityPosition(Vector3 position)
    {
        return new Vector3(position.x * FacilityPlanScale, position.y, position.z * FacilityPlanScale);
    }

    private Transform CreateFloorShell(int floorNumber, string floorName, float baseY)
    {
        GameObject floorObject = new GameObject($"Floor_{floorNumber}_{floorName}");
        floorObject.transform.SetParent(facilityRoot, false);
        Transform floor = floorObject.transform;

        CreateBlock($"{floorNumber}F_Slab", new Vector3(0f, baseY - 0.15f, 0f),
            new Vector3(FacilityWidth, 0.3f, FacilityDepth), floor);

        float wallY = baseY + 2.35f;
        CreateWall($"{floorNumber}F_Perimeter_North", new Vector3(0f, wallY, FacilityDepth * 0.5f), new Vector3(FacilityWidth, 4.7f, 0.35f), floor);
        CreateWall($"{floorNumber}F_Perimeter_West", new Vector3(-FacilityWidth * 0.5f, wallY, 0f), new Vector3(0.35f, 4.7f, FacilityDepth), floor);
        CreateWall($"{floorNumber}F_Perimeter_East", new Vector3(FacilityWidth * 0.5f, wallY, 0f), new Vector3(0.35f, 4.7f, FacilityDepth), floor);
        if (floorNumber == 1)
        {
            CreateWall($"{floorNumber}F_Perimeter_South_West", ScaleFacilityPosition(new Vector3(-12f, wallY, -15f)), new Vector3(28f, 4.7f, 0.35f), floor);
            CreateWall($"{floorNumber}F_Perimeter_South_East", ScaleFacilityPosition(new Vector3(12f, wallY, -15f)), new Vector3(28f, 4.7f, 0.35f), floor);
        }
        else
        {
            CreateWall($"{floorNumber}F_Perimeter_South", new Vector3(0f, wallY, -FacilityDepth * 0.5f),
                new Vector3(FacilityWidth, 4.7f, 0.35f), floor);
        }

        CreateFloorLighting(floorNumber, baseY, floor);
        return floor;
    }

    private void CreateFirstFloorLayout(Transform floor, float baseY)
    {
        CreateRoom(1, "BREAK_ROOM", new Vector2(-14f, 10f), new Vector2(10f, 6f), baseY, RoomDoorSide.South, 2.5f, floor);
        CreateRoom(1, "TOILET", new Vector2(-6.3f, 10.2f), new Vector2(4.5f, 5.5f), baseY, RoomDoorSide.South, 0f, floor);
        CreateRoom(1, "WAREHOUSE", new Vector2(5.5f, 10.2f), new Vector2(5.5f, 5.5f), baseY, RoomDoorSide.South, -1f, floor);
        CreateRoom(1, "2F_PASS_STORAGE", new Vector2(11.5f, 10.2f), new Vector2(5.5f, 5.5f), baseY,
            RoomDoorSide.South, 1f, floor, true, "2F PASS STORAGE");
        CreateRoom(1, "NORTH_EAST_OFFICE", new Vector2(18f, 7.2f), new Vector2(7.5f, 11f), baseY, RoomDoorSide.West, -3f, floor);
        CreateRoom(1, "MEETING_ROOM", new Vector2(8f, 1.5f), new Vector2(8.5f, 6.5f), baseY, RoomDoorSide.West, -1.5f, floor);
        CreateRoom(1, "SOUTH_EAST_OFFICE", new Vector2(16.5f, -7f), new Vector2(10.5f, 8.5f), baseY, RoomDoorSide.West, 2.2f, floor);
        CreateRoom(1, "SECURITY_ROOM", new Vector2(6f, -10f), new Vector2(5.5f, 5f), baseY, RoomDoorSide.West, 0.8f, floor);

        CreateOpenArea(1, "OPEN_OFFICE", new Vector2(-12f, -1.5f), new Vector2(18f, 16f), baseY, floor, 18);
        CreateOpenArea(1, "LOBBY_ENTRANCE", new Vector2(0f, -9f), new Vector2(9f, 10f), baseY, floor, 0);
        CreateElevatorCore(1, baseY, floor);
    }

    private void CreateSecondFloorLayout(Transform floor, float baseY)
    {
        CreateRoom(2, "OPEN_OFFICE", new Vector2(-15f, 9.5f), new Vector2(12f, 8f), baseY, RoomDoorSide.East, -2f, floor);
        CreateRoom(2, "TOILET", new Vector2(-6.5f, 10.5f), new Vector2(5f, 5f), baseY, RoomDoorSide.South, 1f, floor);
        CreateRoom(2, "SECURITY_ANALYSIS", new Vector2(5f, 10.5f), new Vector2(6f, 5f), baseY, RoomDoorSide.South, 0f, floor);
        CreateRoom(2, "3F_PASS_STORAGE", new Vector2(11f, 10.5f), new Vector2(5f, 5f), baseY,
            RoomDoorSide.South, 0f, floor, true, "3F PASS STORAGE");
        CreateRoom(2, "DATA_ROOM", new Vector2(17.5f, 9.5f), new Vector2(7.5f, 7f), baseY, RoomDoorSide.West, -1.5f, floor);
        CreateRoom(2, "OFFICE_4", new Vector2(-17.5f, 1.5f), new Vector2(7f, 6f), baseY, RoomDoorSide.South, 1.5f, floor);
        CreateRoom(2, "OFFICE_5", new Vector2(-10.5f, 1.5f), new Vector2(6.5f, 6f), baseY, RoomDoorSide.South, -1.5f, floor);
        CreateRoom(2, "RECORD_STORAGE", new Vector2(-15f, -7.5f), new Vector2(13f, 7.5f), baseY, RoomDoorSide.East, 1.5f, floor);
        CreateRoom(2, "MONITORING_ROOM", new Vector2(6.5f, 1.8f), new Vector2(7.5f, 8f), baseY, RoomDoorSide.West, -2.2f, floor);
        CreateRoom(2, "OFFICE_1", new Vector2(17.8f, 2.8f), new Vector2(7f, 5.5f), baseY, RoomDoorSide.West, -1f, floor);
        CreateRoom(2, "OFFICE_2", new Vector2(17.8f, -3.2f), new Vector2(7f, 5.5f), baseY, RoomDoorSide.West, 1f, floor);
        CreateRoom(2, "CONFERENCE_ROOM", new Vector2(9.5f, -9f), new Vector2(11f, 6.5f), baseY, RoomDoorSide.West, 1.8f, floor);

        CreateOpenArea(2, "CENTRAL_LOBBY", new Vector2(0f, -7f), new Vector2(12f, 12f), baseY, floor, 0);
        CreateElevatorCore(2, baseY, floor);
    }

    private void CreateThirdFloorLayout(Transform floor, float baseY)
    {
        CreateRoom(3, "EXECUTIVE_ROOM", new Vector2(-16.5f, 8.5f), new Vector2(10f, 10.5f), baseY, RoomDoorSide.East, -3f, floor);
        CreateRoom(3, "MAIN_SERVER_ROOM", new Vector2(-6f, 11f), new Vector2(9.5f, 6f), baseY,
            RoomDoorSide.South, -2f, floor, true, "MAIN SERVER ROOM");
        CreateRoom(3, "MANAGEMENT_OFFICE", new Vector2(4.3f, 11f), new Vector2(8.5f, 6f), baseY, RoomDoorSide.South, 2f, floor);
        CreateRoom(3, "TOILET", new Vector2(15.5f, 11f), new Vector2(8.5f, 6f), baseY, RoomDoorSide.South, -1f, floor);
        CreateRoom(3, "COMMAND_ROOM", new Vector2(-15.5f, -5.2f), new Vector2(11.5f, 14f), baseY, RoomDoorSide.East, 3f, floor);
        CreateRoom(3, "SURVEILLANCE_HUB", new Vector2(8.5f, -5.5f), new Vector2(10.5f, 12.5f), baseY, RoomDoorSide.West, -3f, floor);
        CreateRoom(3, "DOCUMENT_ARCHIVE", new Vector2(17.5f, 2.5f), new Vector2(7f, 7f), baseY, RoomDoorSide.West, -1.5f, floor);
        CreateRoom(3, "MASTER_KEY_STORAGE", new Vector2(17.5f, -5f), new Vector2(7f, 6.5f), baseY,
            RoomDoorSide.West, 1.5f, floor, true, "MASTER KEY STORAGE");

        CreateOpenArea(3, "TOP_FLOOR_LOBBY", new Vector2(0f, -6f), new Vector2(12f, 13f), baseY, floor, 0);
        CreateElevatorCore(3, baseY, floor);
    }

    private void CreateRoom(int floorNumber, string roomName, Vector2 center, Vector2 size, float baseY,
        RoomDoorSide doorSide, float doorOffset, Transform floor, bool locked = false, string accessName = null)
    {
        center *= FacilityPlanScale;
        size *= FacilityPlanScale;
        doorOffset *= FacilityPlanScale;

        GameObject roomObject = new GameObject($"Room_{floorNumber}F_{roomName}");
        roomObject.transform.SetParent(floor, false);
        roomObject.transform.position = new Vector3(center.x, baseY, center.y);

        float wallY = baseY + 2.35f;
        float west = center.x - size.x * 0.5f;
        float east = center.x + size.x * 0.5f;
        float south = center.y - size.y * 0.5f;
        float north = center.y + size.y * 0.5f;

        CreateRoomWallX(floorNumber, roomName, "North", center.x, north, size.x, wallY,
            doorSide == RoomDoorSide.North, doorOffset, roomObject.transform);
        CreateRoomWallX(floorNumber, roomName, "South", center.x, south, size.x, wallY,
            doorSide == RoomDoorSide.South, doorOffset, roomObject.transform);
        CreateRoomWallZ(floorNumber, roomName, "West", west, center.y, size.y, wallY,
            doorSide == RoomDoorSide.West, doorOffset, roomObject.transform);
        CreateRoomWallZ(floorNumber, roomName, "East", east, center.y, size.y, wallY,
            doorSide == RoomDoorSide.East, doorOffset, roomObject.transform);

        if (locked)
        {
            Vector3 doorPosition;
            Vector3 doorScale;
            if (doorSide == RoomDoorSide.North || doorSide == RoomDoorSide.South)
            {
                doorPosition = new Vector3(center.x + doorOffset, baseY + 1.5f,
                    doorSide == RoomDoorSide.North ? north : south);
                doorScale = new Vector3(2.2f, 3f, 0.3f);
            }
            else
            {
                doorPosition = new Vector3(doorSide == RoomDoorSide.East ? east : west,
                    baseY + 1.5f, center.y + doorOffset);
                doorScale = new Vector3(0.3f, 3f, 2.2f);
            }
            CreateAccessDoor($"{floorNumber}F_{roomName}_ACCESS", accessName ?? roomName,
                doorPosition, doorScale, roomObject.transform);
        }

        if (!roomName.Contains("ELEVATOR"))
            CreateRoomFixture(floorNumber, roomName, center, size, baseY, roomObject.transform);
    }

    private void CreateRoomWallX(int floorNumber, string roomName, string sideName, float centerX, float z,
        float length, float wallY, bool hasDoor, float doorOffset, Transform parent)
    {
        if (!hasDoor)
        {
            CreateWall($"{floorNumber}F_{roomName}_{sideName}", new Vector3(centerX, wallY, z),
                new Vector3(length, 4.7f, 0.25f), parent);
            return;
        }

        CreateWallSegmentsAroundGap($"{floorNumber}F_{roomName}_{sideName}", centerX, length, doorOffset,
            (segmentCenter, segmentLength, suffix) => CreateWall(suffix,
                new Vector3(segmentCenter, wallY, z), new Vector3(segmentLength, 4.7f, 0.25f), parent));
    }

    private void CreateRoomWallZ(int floorNumber, string roomName, string sideName, float x, float centerZ,
        float length, float wallY, bool hasDoor, float doorOffset, Transform parent)
    {
        if (!hasDoor)
        {
            CreateWall($"{floorNumber}F_{roomName}_{sideName}", new Vector3(x, wallY, centerZ),
                new Vector3(0.25f, 4.7f, length), parent);
            return;
        }

        CreateWallSegmentsAroundGap($"{floorNumber}F_{roomName}_{sideName}", centerZ, length, doorOffset,
            (segmentCenter, segmentLength, suffix) => CreateWall(suffix,
                new Vector3(x, wallY, segmentCenter), new Vector3(0.25f, 4.7f, segmentLength), parent));
    }

    private void CreateWallSegmentsAroundGap(string name, float center, float length, float doorOffset,
        System.Action<float, float, string> createSegment)
    {
        const float doorWidth = 2.2f;
        float min = center - length * 0.5f;
        float max = center + length * 0.5f;
        float gapCenter = Mathf.Clamp(center + doorOffset, min + doorWidth * 0.5f, max - doorWidth * 0.5f);
        float gapMin = gapCenter - doorWidth * 0.5f;
        float gapMax = gapCenter + doorWidth * 0.5f;
        float firstLength = gapMin - min;
        float secondLength = max - gapMax;

        if (firstLength > 0.1f)
            createSegment(min + firstLength * 0.5f, firstLength, name + "_A");
        if (secondLength > 0.1f)
            createSegment(gapMax + secondLength * 0.5f, secondLength, name + "_B");
    }

    private void CreateRoomFixture(int floorNumber, string roomName, Vector2 center, Vector2 size, float baseY, Transform parent)
    {
        bool computerRoom = roomName.Contains("OFFICE") || roomName.Contains("SECURITY") ||
            roomName.Contains("MONITORING") || roomName.Contains("SURVEILLANCE") ||
            roomName.Contains("COMMAND") || roomName.Contains("MANAGEMENT") ||
            roomName.Contains("EXECUTIVE");

        if (computerRoom)
        {
            int workstationCount = Mathf.Clamp(Mathf.FloorToInt(size.x * size.y / 24f), 2, 8);
            CreateWorkstationGrid($"{floorNumber}F_{roomName}", center, size, baseY, parent, workstationCount);
            return;
        }

        if (roomName.Contains("MEETING") || roomName.Contains("CONFERENCE"))
        {
            CreateBlock($"{floorNumber}F_{roomName}_MeetingTable", new Vector3(center.x, baseY + 0.4f, center.y),
                new Vector3(Mathf.Min(size.x * 0.55f, 5.5f), 0.8f, Mathf.Min(size.y * 0.35f, 2.3f)), parent);
            CreateOfficeWorkstation($"{floorNumber}F_{roomName}_Presentation", new Vector3(center.x, baseY, center.y + size.y * 0.28f), 180f, parent);
            return;
        }

        float fixtureWidth = Mathf.Clamp(size.x * 0.32f, 1.2f, 3.2f);
        float fixtureDepth = Mathf.Clamp(size.y * 0.18f, 0.7f, 1.4f);
        GameObject fixture = CreateBlock($"{floorNumber}F_{roomName}_Fixture", new Vector3(center.x, baseY + 0.4f,
            center.y + size.y * 0.22f), new Vector3(fixtureWidth, 0.8f, fixtureDepth), parent);
        if (roomName.Contains("BREAK"))
            fixture.AddComponent<RoutineActivityPoint>().Configure(RoutineActivityType.Break, 5f, "自然に休憩");
    }

    private void CreateOpenArea(int floorNumber, string areaName, Vector2 center, Vector2 size,
        float baseY, Transform floor, int fixtureCount)
    {
        center *= FacilityPlanScale;
        size *= FacilityPlanScale;

        GameObject area = new GameObject($"Area_{floorNumber}F_{areaName}");
        area.transform.SetParent(floor, false);
        area.transform.position = new Vector3(center.x, baseY, center.y);

        if (fixtureCount > 0)
            CreateWorkstationGrid($"{floorNumber}F_{areaName}", center, size, baseY, area.transform, fixtureCount);
    }

    private void CreateWorkstationGrid(string groupName, Vector2 center, Vector2 size, float baseY,
        Transform parent, int workstationCount)
    {
        int columns = Mathf.Clamp(Mathf.FloorToInt(size.x / 4f), 2, 4);
        int rows = Mathf.CeilToInt(workstationCount / (float)columns);
        float spacingX = Mathf.Min(3.6f, size.x * 0.7f / Mathf.Max(1, columns - 1));
        float spacingZ = Mathf.Min(3.5f, size.y * 0.68f / Mathf.Max(1, rows - 1));
        float startX = center.x - spacingX * (columns - 1) * 0.5f;
        float startZ = center.y - spacingZ * (rows - 1) * 0.5f;

        for (int i = 0; i < workstationCount; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Vector3 position = new Vector3(startX + column * spacingX, baseY, startZ + row * spacingZ);
            CreateOfficeWorkstation($"{groupName}_{i + 1:00}", position, row % 2 == 0 ? 0f : 180f, parent);

            if (i % 6 == 5)
                CreateOfficePlant($"{groupName}_Plant_{i / 6 + 1}", position + new Vector3(1.5f, 0f, 1.1f), parent);
        }
    }

    private void CreateOfficeWorkstation(string workstationName, Vector3 position, float rotationY, Transform parent)
    {
        GameObject root = new GameObject($"Workstation_{workstationName}");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

        GameObject desk = CreateLocalBlock($"{workstationName}_Desk", new Vector3(0f, 0.38f, 0f),
            new Vector3(2.4f, 0.76f, 1.05f), root.transform);
        SetObjectMaterial(desk, officeDeskMaterial);

        GameObject monitor = CreateLocalBlock($"BluffComputer_{workstationName}", new Vector3(0f, 1.08f, 0.12f),
            new Vector3(0.9f, 0.62f, 0.12f), root.transform);
        SetObjectMaterial(monitor, officeMonitorMaterial);
        monitor.AddComponent<RoutineActivityPoint>().Configure(RoutineActivityType.Workstation, 4f, "通常業務");
        CreateLocalBlock($"{workstationName}_MonitorStand", new Vector3(0f, 0.78f, 0.12f),
            new Vector3(0.12f, 0.35f, 0.12f), root.transform);

        GameObject chairSeat = CreateLocalBlock($"{workstationName}_ChairSeat", new Vector3(0f, 0.48f, -1.05f),
            new Vector3(0.72f, 0.18f, 0.72f), root.transform);
        GameObject chairBack = CreateLocalBlock($"{workstationName}_ChairBack", new Vector3(0f, 0.92f, -1.35f),
            new Vector3(0.72f, 0.9f, 0.15f), root.transform);
        SetObjectMaterial(chairSeat, officeChairMaterial);
        SetObjectMaterial(chairBack, officeChairMaterial);
    }

    private void CreateOfficePlant(string plantName, Vector3 position, Transform parent)
    {
        GameObject root = new GameObject(plantName);
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = plantName + "_Pot";
        pot.transform.SetParent(root.transform, false);
        pot.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        pot.transform.localScale = new Vector3(0.35f, 0.28f, 0.35f);
        SetObjectMaterial(pot, officePotMaterial);

        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = plantName + "_Leaves";
        leaves.transform.SetParent(root.transform, false);
        leaves.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        leaves.transform.localScale = new Vector3(0.85f, 1.2f, 0.85f);
        SetObjectMaterial(leaves, officePlantMaterial);
    }

    private GameObject CreateLocalBlock(string objectName, Vector3 localPosition, Vector3 localScale, Transform parent)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = localScale;
        return block;
    }

    private static void SetObjectMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private void CreateElevatorCore(int floorNumber, float baseY, Transform floor)
    {
        CreateRoom(floorNumber, "CENTRAL_ELEVATOR", new Vector2(0f, 1.5f), new Vector2(5.5f, 6f),
            baseY, RoomDoorSide.South, 0f, floor);
    }

    private void CreateFloor(int floorNumber, string floorName, float baseY,
        string southWest, string northWest, string southEast, string northEast)
    {
        GameObject floorObject = new GameObject($"Floor_{floorNumber}_{floorName}");
        floorObject.transform.SetParent(facilityRoot, false);
        Transform floor = floorObject.transform;

        CreateBlock($"{floorNumber}F_Slab", new Vector3(0f, baseY - 0.15f, 0f),
            new Vector3(34f, 0.3f, 26f), floor);

        float wallY = baseY + 2.35f;
        CreateWall($"{floorNumber}F_Perimeter_South", new Vector3(0f, wallY, -13f), new Vector3(34f, 4.7f, 0.35f), floor);
        CreateWall($"{floorNumber}F_Perimeter_North", new Vector3(0f, wallY, 13f), new Vector3(34f, 4.7f, 0.35f), floor);
        CreateWall($"{floorNumber}F_Perimeter_West", new Vector3(-17f, wallY, 0f), new Vector3(0.35f, 4.7f, 26f), floor);
        CreateWall($"{floorNumber}F_Perimeter_East", new Vector3(17f, wallY, 0f), new Vector3(0.35f, 4.7f, 26f), floor);

        CreateCorridorWallSegments(floorNumber, -2f, wallY, floor);
        CreateCorridorWallSegments(floorNumber, 2f, wallY, floor);
        CreateHorizontalDepartmentWall(floorNumber, true, wallY, floor);
        CreateHorizontalDepartmentWall(floorNumber, false, wallY, floor);

        CreateDepartmentZone(floorNumber, southWest, new Vector3(-9f, baseY, -6f), floor);
        CreateDepartmentZone(floorNumber, northWest, new Vector3(-9f, baseY, 6f), floor);
        CreateDepartmentZone(floorNumber, southEast, new Vector3(9f, baseY, -6f), floor);
        CreateDepartmentZone(floorNumber, northEast, new Vector3(9f, baseY, 6f), floor);

        CreateOfficeObstacles(floorNumber, baseY, floor);
        CreateFloorLighting(floorNumber, baseY, floor);

        if (floorNumber == 1)
            CreateAccessDoor("1F_SECURITY_ARCHIVE_ACCESS", "SECURITY ARCHIVE", new Vector3(2f, baseY + 1.5f, 5.5f), new Vector3(0.35f, 3f, 2.6f), floor);
        else if (floorNumber == 2)
            CreateAccessDoor("2F_FINANCE_RECORDS_ACCESS", "FINANCE RECORDS", new Vector3(2f, baseY + 1.5f, 5.5f), new Vector3(0.35f, 3f, 2.6f), floor);
        else
        {
            CreateAccessDoor("3F_EXECUTIVE_ACCESS", "EXECUTIVE OFFICE", new Vector3(-2f, baseY + 1.5f, -4f), new Vector3(0.35f, 3f, 2.6f), floor);
            CreateAccessDoor("3F_SERVER_ACCESS", "SERVER ROOM", new Vector3(2f, baseY + 1.5f, 5.5f), new Vector3(0.35f, 3f, 2.6f), floor);
        }
    }

    private void CreateCorridorWallSegments(int floorNumber, float x, float wallY, Transform parent)
    {
        CreateWall($"{floorNumber}F_Corridor_{x}_South", new Vector3(x, wallY, -9f), new Vector3(0.3f, 4.7f, 7f), parent);
        CreateWall($"{floorNumber}F_Corridor_{x}_Center", new Vector3(x, wallY, 0.75f), new Vector3(0.3f, 4.7f, 6.5f), parent);
        CreateWall($"{floorNumber}F_Corridor_{x}_North", new Vector3(x, wallY, 9.75f), new Vector3(0.3f, 4.7f, 5.5f), parent);
    }

    private void CreateHorizontalDepartmentWall(int floorNumber, bool west, float wallY, Transform parent)
    {
        float sign = west ? -1f : 1f;
        CreateWall($"{floorNumber}F_DepartmentSplit_{(west ? "West" : "East")}_Outer",
            new Vector3(sign * 13.75f, wallY, 0f), new Vector3(5.5f, 4.7f, 0.3f), parent);
        CreateWall($"{floorNumber}F_DepartmentSplit_{(west ? "West" : "East")}_Inner",
            new Vector3(sign * 5.25f, wallY, 0f), new Vector3(6.5f, 4.7f, 0.3f), parent);
    }

    private void CreateOfficeObstacles(int floorNumber, float baseY, Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(-12f, baseY + 0.4f, -7f), new Vector3(-8f, baseY + 0.4f, -7f),
            new Vector3(-12f, baseY + 0.4f, 7f), new Vector3(-8f, baseY + 0.4f, 7f),
            new Vector3(8f, baseY + 0.4f, -7f), new Vector3(12f, baseY + 0.4f, -7f),
            new Vector3(8f, baseY + 0.4f, 7f), new Vector3(12f, baseY + 0.4f, 7f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 scale = i % 2 == 0 ? new Vector3(2.4f, 0.8f, 1.1f) : new Vector3(1.1f, 1.4f, 2.2f);
            CreateBlock($"{floorNumber}F_OfficeFixture_{i + 1}", positions[i], scale, parent);
        }
    }

    private void CreateDepartmentZone(int floorNumber, string departmentName, Vector3 center, Transform parent)
    {
        GameObject zone = new GameObject($"Department_{floorNumber}F_{departmentName}");
        zone.transform.SetParent(parent, false);
        zone.transform.position = center;
    }

    private void CreateFloorLighting(int floorNumber, float baseY, Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(-16f, baseY + 4.25f, -9f),
            new Vector3(0f, baseY + 4.25f, -9f),
            new Vector3(16f, baseY + 4.25f, -9f),
            new Vector3(-16f, baseY + 4.25f, 8f),
            new Vector3(0f, baseY + 4.25f, 8f),
            new Vector3(16f, baseY + 4.25f, 8f),
            new Vector3(0f, baseY + 4.25f, 0f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject lightObject = new GameObject($"{floorNumber}F_CeilingLight_{i + 1}");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = ScaleFacilityPosition(positions[i]);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 16f;
            light.intensity = 2.2f;
            light.color = new Color(0.86f, 0.93f, 1f);
            light.shadows = LightShadows.None;
        }
    }

    private void CreateAccessDoor(string objectName, string areaName, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject door = CreateBlock(objectName, position, scale, parent);
        door.AddComponent<AccessControlledDoor>().Configure(areaName);
    }

    private void CreateElevatorPanels()
    {
        CreateElevatorPanel("1F_Elevator_To_2F", 2, new Vector3(-1.6f, 1.2f, -1.7f), new Vector3(0f, 5.9f, -3.8f));
        CreateElevatorPanel("2F_Elevator_To_1F", 1, new Vector3(-1.6f, 6.2f, -1.7f), new Vector3(0f, 0.9f, -3.8f));
        CreateElevatorPanel("2F_Elevator_To_3F", 3, new Vector3(1.6f, 6.2f, -1.7f), new Vector3(0f, 10.9f, -3.8f));
        CreateElevatorPanel("3F_Elevator_To_2F", 2, new Vector3(1.6f, 11.2f, -1.7f), new Vector3(0f, 5.9f, -3.8f));
    }

    private void CreateElevatorPanel(string objectName, int destinationFloor, Vector3 position, Vector3 destination)
    {
        position = ScaleFacilityPosition(position);
        destination = ScaleFacilityPosition(destination);
        GameObject panel = CreateBlock(objectName, position, new Vector3(0.55f, 0.8f, 0.25f), facilityRoot);
        panel.AddComponent<ElevatorPanel>().Configure(destinationFloor, destination);
    }

    private GameObject CreateBlock(string objectName, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        return block;
    }

    private void CreateWall(string objectName, Vector3 position, Vector3 scale, Transform parent)
    {
        CreateBlock(objectName, position, scale, parent);
    }

    private void CreateTestTerminals()
    {
        CreateTerminal(1, new Vector3(-14f, 0.6f, -2f));
        CreateTerminal(2, new Vector3(18f, FloorHeight + 0.6f, 8f));
        CreateTerminal(3, new Vector3(-6f, FloorHeight * 2f + 0.6f, 11f));
    }
    
    private void CreateTerminal(int id, Vector3 position)
    {
        position = ScaleFacilityPosition(position);

        GameObject station = new GameObject($"InteractiveWorkstation_{id}");
        station.transform.SetParent(facilityRoot, false);
        station.transform.position = new Vector3(position.x, position.y - 0.6f, position.z);

        GameObject desk = CreateLocalBlock($"Terminal_{id}_Desk", new Vector3(0f, 0.38f, 0f),
            new Vector3(2.4f, 0.76f, 1.05f), station.transform);
        SetObjectMaterial(desk, officeDeskMaterial);

        GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        terminal.name = string.Format("Terminal_{0}", id);
        terminal.transform.SetParent(station.transform, false);
        terminal.transform.localPosition = new Vector3(0f, 1.08f, 0.12f);
        terminal.transform.localScale = new Vector3(0.9f, 0.62f, 0.12f);
        SetObjectMaterial(terminal, officeInteractiveMonitorMaterial);

        CreateLocalBlock($"Terminal_{id}_MonitorStand", new Vector3(0f, 0.78f, 0.12f),
            new Vector3(0.12f, 0.35f, 0.12f), station.transform);
        GameObject chair = CreateLocalBlock($"Terminal_{id}_Chair", new Vector3(0f, 0.65f, -1.15f),
            new Vector3(0.72f, 1.15f, 0.62f), station.transform);
        SetObjectMaterial(chair, officeChairMaterial);

        DestroySetupObject(terminal.GetComponent<BoxCollider>());
        DestroySetupObject(terminal.GetComponent<Rigidbody>());
        
        // 当たり判定は見た目のメッシュより大きめに取り、視点の高さ(約1.6m)から
        // 水平に見てもRaycastが当たるようにする（見た目の1m角メッシュのままだと目線より低く外れる）
        var collider = terminal.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = new Vector3(2.2f, 2.5f, 8f);

        var terminalScript = terminal.AddComponent<Terminal>();
        terminalScript.SetTerminalId(id);

        if (id == 1)
            SetLayerRecursively(station, FirstFloorMapLayer);
    }
    
    private void CreateExitPoint()
    {
        GameObject exitPoint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        exitPoint.name = "ExitPoint";
        exitPoint.transform.SetParent(facilityRoot, false);
        exitPoint.transform.position = ScaleFacilityPosition(new Vector3(0f, 0.5f, -13.2f));
        exitPoint.transform.localScale = new Vector3(1, 0.5f, 1);
        exitPoint.layer = FirstFloorMapLayer;
        DestroySetupObject(exitPoint.GetComponent<CapsuleCollider>());
        DestroySetupObject(exitPoint.GetComponent<Rigidbody>());
        
        // Terminalと同様、視点の高さから水平に見てもRaycastが当たるよう当たり判定を広げる
        var collider = exitPoint.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 0.5f, 0);
        collider.height = 4f;
        collider.radius = 2f;

        exitPoint.AddComponent<ExitPoint>();
    }

    private void CreateSurveillanceNetwork()
    {
        DelayedSurveillance surveillance = FindAnyObjectByType<DelayedSurveillance>(FindObjectsInactive.Include);
        if (surveillance == null)
            return;

        surveillance.ClearCameraAreas();
        GameObject network = new GameObject("SurveillanceNetwork_3Floor");

        CreateSurveillanceCamera(surveillance, network.transform, "1F_LOBBY_ENTRANCE", 8f,
            new Vector3(-2.2f, 4.1f, -14.2f), new Vector3(0f, 1f, -7f), new Vector3(0f, 0f, -9f), 8f);
        CreateSurveillanceCamera(surveillance, network.transform, "1F_OPEN_OFFICE", 22f,
            new Vector3(-20.8f, 4.1f, -12.8f), new Vector3(-12f, 1f, -2f), new Vector3(-12f, 0f, -2f), 10f);
        CreateSurveillanceCamera(surveillance, network.transform, "2F_CENTRAL_LOBBY", 12f,
            new Vector3(-2.2f, 9.1f, -14.2f), new Vector3(0f, 6f, -5f), new Vector3(0f, 5f, -7f), 9f);
        CreateSurveillanceCamera(surveillance, network.transform, "2F_DATA_ROOM", 35f,
            new Vector3(20.8f, 9.1f, 12.8f), new Vector3(17f, 6f, 7f), new Vector3(17.5f, 5f, 8f), 7f);
        CreateSurveillanceCamera(surveillance, network.transform, "3F_EXECUTIVE_CORRIDOR", 18f,
            new Vector3(-10.8f, 14.1f, 7f), new Vector3(-2f, 11f, 5.5f), new Vector3(-5f, 10f, 5.5f), 9f);
        CreateSurveillanceCamera(surveillance, network.transform, "3F_SURVEILLANCE_HUB", 4f,
            new Vector3(11.8f, 14.1f, -11.2f), new Vector3(7f, 11f, -5f), new Vector3(7f, 10f, -5.5f), 7f);
    }

    [ContextMenu("Rebuild Three Floor Facility")]
    public void RebuildThreeFloorFacility()
    {
        EnsureDisplayRenderingCamera();
        RemovePreviousFacilityObjects();

        DelayedSurveillance surveillance = FindAnyObjectByType<DelayedSurveillance>(FindObjectsInactive.Include);
        surveillance?.ClearCameraAreas();

        Camera mapCamera = GameObject.Find("MapCamera")?.GetComponent<Camera>();
        if (mapCamera != null)
        {
            mapCamera.transform.position = new Vector3(0f, 18f, 0f);
            mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 31f;
            mapCamera.cullingMask = 1 << FirstFloorMapLayer;
        }

        CreateFacilityLayout();
        CreateTestTerminals();
        CreateExitPoint();
        CreateSurveillanceNetwork();
        CreateOrganizerManagementRoom();

        SpyController spy = FindAnyObjectByType<SpyController>(FindObjectsInactive.Include);
        if (spy != null)
            spy.transform.position = SpySpawnPosition;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void RemovePreviousFacilityObjects()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.parent != null)
                continue;

            string objectName = candidate.name;
            bool isLegacyFacility = objectName == "Floor" || objectName.StartsWith("Wall") ||
                objectName.StartsWith("Terminal_") || objectName == "ExitPoint" ||
                objectName.StartsWith("SurveillanceCamera_");
            bool isCurrentFacility = objectName == "Facility_3Floor" || objectName == "SurveillanceNetwork_3Floor" ||
                objectName == "OrganizerManagementRoom" || objectName == "ManagementRoomCamera";
            if (isLegacyFacility || isCurrentFacility)
                DestroySetupObject(candidate.gameObject);
        }
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

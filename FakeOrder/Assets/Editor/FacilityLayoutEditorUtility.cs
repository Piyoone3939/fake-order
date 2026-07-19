using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;

/// <summary>
/// 3階建て施設を編集可能なシーンオブジェクトとして焼き込む。
/// </summary>
public static class FacilityLayoutEditorUtility
{
    private const string SpyBotPlayValidationKey = "FakeOrder.SpyBotPlayValidation";
    private const string PrototypeScenePath = "Assets/Scenes/GamePrototype.unity";
    private const string NavigationFolder = "Assets/Navigation";
    private static readonly string[] FloorObjectNames =
    {
        "Floor_1_GENERAL_FLOOR", "Floor_2_IMPORTANT_DATA_FLOOR", "Floor_3_TOP_SECURITY_FLOOR"
    };
    private static readonly string[] FloorNavMeshPaths =
    {
        NavigationFolder + "/FirstFloorNavMesh.asset",
        NavigationFolder + "/SecondFloorNavMesh.asset",
        NavigationFolder + "/ThirdFloorNavMesh.asset"
    };

    [InitializeOnLoadMethod]
    private static void EnsureManagementRoomAfterScriptReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != PrototypeScenePath)
                return;

            GamePrototypeSetup setup = Object.FindAnyObjectByType<GamePrototypeSetup>(FindObjectsInactive.Include);
            if (setup == null)
                return;

            GameObject room = GameObject.Find("OrganizerManagementRoom");
            if (room == null || room.transform.Find("ManagementRoom_Interactive_v3") == null ||
                GameObject.Find("ManagementRoomCamera") == null)
            {
                setup.RebuildOrganizerManagementRoom();
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("Organizer management room added to the prototype scene.");
            }

            Camera managementCamera = GameObject.Find("ManagementRoomCamera")?.GetComponent<Camera>();
            if (managementCamera != null)
            {
                string previewPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/ManagementRoomCamera.png"));
                CaptureCamera(managementCamera, previewPath);
                Debug.Log($"Management room preview captured: {previewPath}");
            }
        };
    }

    [InitializeOnLoadMethod]
    private static void ResumeSpyBotPlayValidation()
    {
        if (!SessionState.GetBool(SpyBotPlayValidationKey, false))
            return;
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying)
                return;
            EditorApplication.update -= MonitorSpyBotPlayValidation;
            EditorApplication.update += MonitorSpyBotPlayValidation;
        };
    }

    [MenuItem("Fake Order/Facility/Rebuild Three Floor Layout")]
    public static void RebuildInCurrentScene()
    {
        GamePrototypeSetup setup = Object.FindAnyObjectByType<GamePrototypeSetup>(FindObjectsInactive.Include);
        if (setup == null)
            throw new MissingReferenceException("GamePrototypeSetup was not found in the current scene.");

        setup.RebuildThreeFloorFacility();
        setup.BuildFacilityNavMeshes();
        PersistFacilityNavMeshes();
        EditorUtility.SetDirty(setup);
        EditorSceneManager.MarkSceneDirty(setup.gameObject.scene);
        Selection.activeGameObject = GameObject.Find("Facility_3Floor");
    }

    private static void PersistFacilityNavMeshes()
    {
        if (!AssetDatabase.IsValidFolder(NavigationFolder))
            AssetDatabase.CreateFolder("Assets", "Navigation");

        for (int i = 0; i < FloorObjectNames.Length; i++)
        {
            GameObject floor = GameObject.Find(FloorObjectNames[i]);
            NavMeshSurface surface = floor != null ? floor.GetComponent<NavMeshSurface>() : null;
            if (surface == null || surface.navMeshData == null)
                throw new MissingComponentException($"NavMesh data was not generated: {FloorObjectNames[i]}");

            AssetDatabase.DeleteAsset(FloorNavMeshPaths[i]);
            AssetDatabase.CreateAsset(surface.navMeshData, FloorNavMeshPaths[i]);
            EditorUtility.SetDirty(surface);
        }
        AssetDatabase.SaveAssets();
    }

    public static void RebuildSaveForCommandLine()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        RebuildInCurrentScene();
        ValidateInteractiveOrganizerRoom();
        ValidateComplexFacilityLayout();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(new[] { PrototypeScenePath },
            ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
        CaptureCameraPreviews();
        Debug.Log("Three-floor facility layout rebuilt and saved.");
    }

    public static void VerifySpyBotPlayModeForCommandLine()
    {
        EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        SessionState.SetBool(SpyBotPlayValidationKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void MonitorSpyBotPlayValidation()
    {
        if (!EditorApplication.isPlaying)
            return;

        if (Object.FindAnyObjectByType<GameManager>() == null ||
            Object.FindAnyObjectByType<SpyBotController>() == null)
            return;
        EditorApplication.update -= MonitorSpyBotPlayValidation;
        SessionState.SetBool(SpyBotPlayValidationKey, false);
        new GameObject("SpyBotPlayModeProbe").AddComponent<SpyBotPlayModeProbe>();
    }

    private static void ValidateInteractiveOrganizerRoom()
    {
        GameObject organizer = GameObject.Find("Organizer");
        Camera playerCamera = GameObject.Find("ManagementRoomCamera")?.GetComponent<Camera>();
        Camera displayBackdropCamera = GameObject.Find("DisplayBackdropCamera")?.GetComponent<Camera>();
        GameObject room = GameObject.Find("OrganizerManagementRoom");
        GameObject terminal = GameObject.Find("DeskMonitorScreen");

        if (organizer == null || organizer.GetComponent<CharacterController>() == null)
            throw new MissingComponentException("Organizer CharacterController was not generated.");
        if (playerCamera == null || playerCamera.transform.parent != organizer.transform || playerCamera.targetTexture != null)
            throw new MissingComponentException("Organizer first-person camera is not configured for direct display.");
        if (displayBackdropCamera == null || !displayBackdropCamera.enabled || displayBackdropCamera.targetTexture != null ||
            displayBackdropCamera.targetDisplay != 0 || displayBackdropCamera.cullingMask != 0 || displayBackdropCamera.depth > -50f)
            throw new MissingComponentException("Display 1 fallback camera is not configured.");
        if (room == null || room.transform.Find("ManagementRoom_Interactive_v3") == null)
            throw new MissingReferenceException("Interactive management room marker was not generated.");
        if (terminal == null || terminal.GetComponent<Collider>() == null)
            throw new MissingComponentException("Desk surveillance terminal is not interactable.");

        string[] additionalStations =
        {
            "CommandPlanningTable", "CommunicationsConsole", "EvidenceCabinet", "EmergencyControlPanel"
        };
        foreach (string stationName in additionalStations)
        {
            if (GameObject.Find(stationName) == null)
                throw new MissingReferenceException($"Management room station was not generated: {stationName}");
        }

        Debug.Log("Interactive organizer room validation passed.");
    }

    private static void ValidateComplexFacilityLayout()
    {
        string[] requiredRooms =
        {
            "Room_1F_BREAK_ROOM", "Room_1F_2F_PASS_STORAGE", "Room_1F_MEETING_ROOM", "Room_1F_SECURITY_ROOM",
            "Room_2F_SECURITY_ANALYSIS", "Room_2F_3F_PASS_STORAGE", "Room_2F_DATA_ROOM", "Room_2F_MONITORING_ROOM",
            "Room_2F_RECORD_STORAGE", "Room_3F_EXECUTIVE_ROOM", "Room_3F_MAIN_SERVER_ROOM",
            "Room_3F_COMMAND_ROOM", "Room_3F_SURVEILLANCE_HUB", "Room_3F_MASTER_KEY_STORAGE"
        };
        foreach (string roomName in requiredRooms)
        {
            if (GameObject.Find(roomName) == null)
                throw new MissingReferenceException($"Required floor-plan room was not generated: {roomName}");
        }

        AccessControlledDoor[] accessDoors = Object.FindObjectsByType<AccessControlledDoor>(FindObjectsInactive.Include);
        ElevatorPanel[] elevatorPanels = Object.FindObjectsByType<ElevatorPanel>(FindObjectsInactive.Include);
        DelayedSurveillance surveillance = Object.FindAnyObjectByType<DelayedSurveillance>(FindObjectsInactive.Include);
        Terminal[] terminals = Object.FindObjectsByType<Terminal>(FindObjectsInactive.Include);
        OfficeNpcController[] npcs = Object.FindObjectsByType<OfficeNpcController>(FindObjectsInactive.Include);
        RoutineActivityPoint[] routinePoints = Object.FindObjectsByType<RoutineActivityPoint>(FindObjectsInactive.Include);
        SpyController spy = Object.FindAnyObjectByType<SpyController>(FindObjectsInactive.Include);
        SpyDisguiseController disguise = spy != null ? spy.GetComponent<SpyDisguiseController>() : null;
        SpyBotController spyBot = spy != null ? spy.GetComponent<SpyBotController>() : null;
        GameObject firstFloor = GameObject.Find("Floor_1_GENERAL_FLOOR");
        Camera mapCamera = GameObject.Find("MapCamera")?.GetComponent<Camera>();
        GameObject firstFloorSlab = GameObject.Find("1F_Slab");
        int bluffComputerCount = 0;
        int[] employeeCounts = new int[3];
        int[] guardCounts = new int[3];
        var employeeAppearanceVariants = new HashSet<int>();
        foreach (Transform item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (item.name.StartsWith("BluffComputer_"))
                bluffComputerCount++;
        }
        foreach (OfficeNpcController npc in npcs)
        {
            int floorIndex = Mathf.Clamp(npc.GetFloorNumber() - 1, 0, 2);
            if (npc.GetNpcRole() == OfficeNpcController.NpcRole.SecurityGuard)
                guardCounts[floorIndex]++;
            else
            {
                employeeCounts[floorIndex]++;
                employeeAppearanceVariants.Add(npc.GetAppearanceVariant());
            }

            Vector3 sameFloorCommand = new Vector3(0f, floorIndex * 5f + 0.6f, 0f);
            Vector3 otherFloorCommand = new Vector3(0f, ((floorIndex + 1) % 3) * 5f + 0.6f, 0f);
            bool isGuard = npc.GetNpcRole() == OfficeNpcController.NpcRole.SecurityGuard;
            if (npc.CanReactToCommand(CommandType.SecurityMovement, sameFloorCommand) != isGuard ||
                npc.CanReactToCommand(CommandType.InspectionOrder, sameFloorCommand) == isGuard ||
                !npc.CanReactToCommand(CommandType.EmergencyOrder, sameFloorCommand) ||
                npc.CanReactToCommand(CommandType.LeakInformation, sameFloorCommand) ||
                npc.CanReactToCommand(CommandType.EmergencyOrder, otherFloorCommand))
                throw new MissingComponentException($"Forged-command reaction rules are invalid: {npc.name}");
        }

        if (accessDoors.Length != 4)
            throw new MissingComponentException($"Expected 4 access-controlled rooms, found {accessDoors.Length}.");
        if (elevatorPanels.Length != 4)
            throw new MissingComponentException($"Expected 4 elevator panels, found {elevatorPanels.Length}.");
        if (surveillance == null || surveillance.GetCameraCount() != 6)
            throw new MissingComponentException("Expected 6 surveillance camera areas.");
        for (int cameraIndex = 0; cameraIndex < surveillance.GetCameraCount(); cameraIndex++)
        {
            Vector3 areaCenter = surveillance.GetAreaCenter(cameraIndex);
            if (!surveillance.IsPositionInsideArea(cameraIndex, areaCenter) ||
                surveillance.IsPositionInsideArea(cameraIndex, areaCenter + Vector3.up * 5f))
                throw new MissingComponentException(
                    $"Delayed evidence area has invalid floor isolation: camera {cameraIndex + 1}.");
        }
        if (terminals.Length != 3)
            throw new MissingComponentException($"Expected 3 information terminals, found {terminals.Length}.");
        if (firstFloor == null || firstFloor.layer != 30 || mapCamera == null || mapCamera.cullingMask != (1 << 30))
            throw new MissingComponentException("The overview map is not isolated to the first-floor layer.");
        if (firstFloorSlab == null || firstFloorSlab.transform.localScale.x < 60f)
            throw new MissingComponentException("The enlarged office floor was not generated.");
        if (bluffComputerCount < 40)
            throw new MissingComponentException($"Expected at least 40 bluff computers, found {bluffComputerCount}.");
        if (routinePoints.Length < bluffComputerCount + 1 ||
            !System.Array.Exists(routinePoints, point => point.GetActivityType() == RoutineActivityType.Break))
            throw new MissingComponentException("Workstation and break-room routine activities were not generated.");
        if (disguise == null || !disguise.HasVisibleDisguise() ||
            spy.GetComponentInChildren<Camera>(true).cullingMask == -1 ||
            (spy.GetComponentInChildren<Camera>(true).cullingMask & (1 << SpyDisguiseController.DisguiseVisualLayer)) != 0)
            throw new MissingComponentException("Spy employee disguise is not configured for surveillance-only rendering.");
        if (spyBot == null || !spyBot.ValidateSceneObjectives())
            throw new MissingComponentException("Organizer test spy bot objectives are incomplete.");
        if (employeeAppearanceVariants.Count < 3)
            throw new MissingComponentException("Employee uniform appearance variants were not distributed.");
        int[] expectedEmployees = { 6, 6, 4 };
        int[] expectedGuards = { 2, 2, 3 };
        for (int i = 0; i < 3; i++)
        {
            if (employeeCounts[i] != expectedEmployees[i] || guardCounts[i] != expectedGuards[i])
                throw new MissingComponentException($"Unexpected NPC count on floor {i + 1}: " +
                    $"{employeeCounts[i]} employees, {guardCounts[i]} guards.");
        }

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        foreach (string floorObjectName in FloorObjectNames)
        {
            NavMeshSurface surface = GameObject.Find(floorObjectName)?.GetComponent<NavMeshSurface>();
            if (surface == null || surface.navMeshData == null)
                throw new MissingComponentException($"Floor NavMesh was not baked: {floorObjectName}");
        }
        if (triangulation.vertices.Length == 0)
            throw new MissingComponentException("Facility NavMesh triangulation is empty.");
        var routeErrors = new List<string>();
        foreach (OfficeNpcController npc in npcs)
        {
            if (!NavMesh.SamplePosition(npc.transform.position, out NavMeshHit startHit, 4f, NavMesh.AllAreas))
            {
                routeErrors.Add($"{npc.name}: spawn");
                continue;
            }

            Vector3 previous = startHit.position;
            Vector3[] route = npc.GetPatrolRoute();
            for (int waypointIndex = 0; waypointIndex < route.Length; waypointIndex++)
            {
                Vector3 waypoint = route[waypointIndex];
                if (!NavMesh.SamplePosition(waypoint, out NavMeshHit waypointHit, 3f, NavMesh.AllAreas))
                {
                    routeErrors.Add($"{npc.name}: waypoint {waypointIndex}");
                    break;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(previous, waypointHit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    routeErrors.Add($"{npc.name}: segment {waypointIndex}");
                    break;
                }
                previous = waypointHit.position;
            }
        }
        if (routeErrors.Count > 0)
            throw new MissingComponentException("Unreachable NPC patrol routes: " + string.Join(", ", routeErrors));

        Terminal[] orderedTerminals = Object.FindObjectsByType<Terminal>(FindObjectsInactive.Include);
        System.Array.Sort(orderedTerminals,
            (left, right) => left.GetTerminalId().CompareTo(right.GetTerminalId()));
        Vector3 elevator1 = new Vector3(0f, 0f, -10f);
        Vector3 elevator2 = new Vector3(0f, 5f, -10f);
        Vector3 elevator3 = new Vector3(0f, 10f, -10f);
        ValidateCompletePath("bot start -> terminal 1", new Vector3(0f, 0f, -11.5f), orderedTerminals[0].transform.position);
        ValidateCompletePath("terminal 1 -> 1F elevator", orderedTerminals[0].transform.position, elevator1);
        ValidateNavMeshEndpoint("2F elevator", elevator2);
        ValidateNavMeshEndpoint("terminal 2 restricted area", orderedTerminals[1].transform.position);
        ValidateNavMeshEndpoint("3F elevator", elevator3);
        ValidateNavMeshEndpoint("terminal 3 restricted area", orderedTerminals[2].transform.position);
        ValidateCompletePath("1F elevator -> exit", elevator1,
            Object.FindAnyObjectByType<ExitPoint>(FindObjectsInactive.Include).transform.position);

        Debug.Log($"Complex three-floor layout validation passed ({bluffComputerCount} bluff computers, " +
            $"16 employees, 7 guards, {triangulation.vertices.Length} NavMesh vertices).");
    }

    private static void ValidateCompletePath(string label, Vector3 start, Vector3 destination)
    {
        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 4f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out NavMeshHit destinationHit, 4f, NavMesh.AllAreas))
            throw new MissingComponentException($"Spy bot path endpoint is outside NavMesh: {label}.");
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(startHit.position, destinationHit.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
            throw new MissingComponentException($"Spy bot path is unreachable: {label}.");
    }

    private static void ValidateNavMeshEndpoint(string label, Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out _, 4f, NavMesh.AllAreas))
            throw new MissingComponentException($"Spy bot access-bypass endpoint is outside NavMesh: {label}.");
    }

    private static void CaptureCameraPreviews()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), "fake-order", "facility-previews");
        Directory.CreateDirectory(outputDirectory);

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (camera.name != "MapCamera" && camera.name != "ManagementRoomCamera" &&
                !camera.name.StartsWith("SurveillanceCamera_"))
                continue;
            CaptureCamera(camera, Path.Combine(outputDirectory, camera.name + ".png"));
        }

        Debug.Log($"Facility camera previews captured: {outputDirectory}");
    }

    private static void CaptureCamera(Camera camera, string outputPath)
    {
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);

        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
        }
    }
}

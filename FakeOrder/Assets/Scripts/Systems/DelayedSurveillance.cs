using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 遅延監視カメラシステム
/// エリアごとに異なる遅延時間でカメラ映像を表示する。
/// 各カメラは liveRenderTexture に毎フレーム自動描画され続け、
/// 一定間隔(snapshotInterval)ごとにGPU上でリングバッファへコピーすることで遅延映像を再現する
/// （CPUへのReadPixels/Texture2D生成は行わない）。
/// </summary>
public class DelayedSurveillance : MonoBehaviour
{
    [System.Serializable]
    public class CameraArea
    {
        public string areaName;
        public float delayTime; // 秒
        public Camera surveillanceCamera;
        public RenderTexture liveRenderTexture; // カメラが毎フレーム描画し続ける先
        public Vector3 areaCenter;
        public float areaRadius;

        [System.NonSerialized] public RenderTexture[] ringBuffer;
        [System.NonSerialized] public float[] captureTimestamps;
        [System.NonSerialized] public int writeIndex;
        [System.NonSerialized] public int filledCount;
        [System.NonSerialized] public float captureTimer;
    }

    [SerializeField] private List<CameraArea> cameraAreas = new List<CameraArea>();
    [SerializeField] private float snapshotInterval = 0.5f;

    public void Initialize()
    {
        foreach (var area in cameraAreas)
        {
            if (area.liveRenderTexture == null) continue;

            int slotCount = Mathf.Max(1, Mathf.CeilToInt(area.delayTime / snapshotInterval) + 1);
            area.ringBuffer = new RenderTexture[slotCount];
            area.captureTimestamps = new float[slotCount];

            var desc = area.liveRenderTexture.descriptor;
            desc.depthBufferBits = 0; // 表示専用のコピー先なのでdepthは不要（メモリ削減）

            for (int s = 0; s < slotCount; s++)
            {
                var rt = new RenderTexture(desc);
                rt.Create();
                area.ringBuffer[s] = rt;
            }

            CaptureSnapshot(area); // 起動直後に1枚確保し、初期表示が空にならないようにする
        }

        Debug.Log($"✓ DelayedSurveillance initialized with {cameraAreas.Count} cameras");
    }

    private void Update()
    {
        for (int i = 0; i < cameraAreas.Count; i++)
        {
            var area = cameraAreas[i];
            if (area.ringBuffer == null) continue;

            area.captureTimer += Time.deltaTime;
            if (area.captureTimer >= snapshotInterval)
            {
                area.captureTimer -= snapshotInterval; // ドリフト防止のため減算
                CaptureSnapshot(area);
            }
        }
    }

    private void CaptureSnapshot(CameraArea area)
    {
        Graphics.CopyTexture(area.liveRenderTexture, area.ringBuffer[area.writeIndex]);
        area.captureTimestamps[area.writeIndex] = Time.time;

        area.writeIndex = (area.writeIndex + 1) % area.ringBuffer.Length;
        area.filledCount = Mathf.Min(area.filledCount + 1, area.ringBuffer.Length);
    }

    private int ComputeReadIndex(CameraArea area)
    {
        int latestIndex = (area.writeIndex - 1 + area.ringBuffer.Length) % area.ringBuffer.Length;
        int delaySlots = Mathf.Min(
            Mathf.RoundToInt(area.delayTime / snapshotInterval),
            area.filledCount - 1);
        return (latestIndex - delaySlots + area.ringBuffer.Length) % area.ringBuffer.Length;
    }

    public Texture GetDelayedFrame(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= cameraAreas.Count) return null;
        var area = cameraAreas[areaIndex];
        if (area.ringBuffer == null || area.filledCount == 0) return null;
        return area.ringBuffer[ComputeReadIndex(area)];
    }

    public float GetDelayedFrameTimestamp(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= cameraAreas.Count) return Time.time;
        var area = cameraAreas[areaIndex];
        if (area.ringBuffer == null || area.filledCount == 0) return Time.time;
        return area.captureTimestamps[ComputeReadIndex(area)];
    }

    public string GetAreaInfo(int areaIndex)
    {
        if (areaIndex >= 0 && areaIndex < cameraAreas.Count)
        {
            CameraArea area = cameraAreas[areaIndex];
            return $"{area.areaName} (遅延: {area.delayTime}秒)";
        }
        return "Unknown Area";
    }

    /// <summary>
    /// ワールド座標がどの監視エリアに属するか判定する。該当エリアが無ければnull（呼び出し側で「盲点エリア」等に変換）
    /// </summary>
    public string GetAreaNameForPosition(Vector3 worldPos)
    {
        foreach (var area in cameraAreas)
        {
            float dx = worldPos.x - area.areaCenter.x;
            float dz = worldPos.z - area.areaCenter.z;
            if (dx * dx + dz * dz <= area.areaRadius * area.areaRadius)
                return area.areaName;
        }
        return null;
    }

    public Vector3 GetAreaCenter(int areaIndex)
    {
        return areaIndex >= 0 && areaIndex < cameraAreas.Count ? cameraAreas[areaIndex].areaCenter : Vector3.zero;
    }

    public void AddCameraArea(CameraArea area)
    {
        cameraAreas.Add(area);
    }

    public int GetCameraCount()
    {
        return cameraAreas.Count;
    }

    private void OnDestroy()
    {
        foreach (var area in cameraAreas)
        {
            if (area.ringBuffer == null) continue;
            foreach (var rt in area.ringBuffer)
            {
                if (rt != null)
                {
                    rt.Release();
                    Destroy(rt);
                }
            }
        }
    }
}

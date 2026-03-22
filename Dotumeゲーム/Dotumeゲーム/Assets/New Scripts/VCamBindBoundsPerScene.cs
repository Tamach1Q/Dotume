// Assets/VCamAutoFix2D.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class VCamAutoFix2D : MonoBehaviour
{
    public string boundsTag = "CameraBounds";   // 各シーンの枠に付けるタグ
    public float confinerDamping = 0.2f;

    CinemachineConfiner2D confiner;

    void Awake()
    {
        confiner = GetComponent<CinemachineConfiner2D>();
        if (!confiner) confiner = gameObject.AddComponent<CinemachineConfiner2D>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        StartCoroutine(RebindAfterSceneChange());
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    void OnSceneLoaded(Scene s, LoadSceneMode m) => StartCoroutine(RebindAfterSceneChange());
    void OnActiveSceneChanged(Scene a, Scene b)   => StartCoroutine(RebindAfterSceneChange());

    IEnumerator RebindAfterSceneChange()
    {
        // 生成完了を少し待つ（AdditiveやSpawn遅延に対応）
        yield return null;
        yield return new WaitForEndOfFrame();

        // ★アクティブシーンの中だけ★から CameraBounds を探す
        var active = SceneManager.GetActiveScene();
        var roots  = active.GetRootGameObjects();
        var shapes = roots
            .SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c =>
                c && c.enabled &&
                c.gameObject.activeInHierarchy &&
                c.gameObject.CompareTag(boundsTag) &&
                (c is PolygonCollider2D || c is CompositeCollider2D))
            .ToArray();

        if (shapes.Length == 0)
        {
            confiner.m_BoundingShape2D = null;
            Debug.LogWarning("[VCam] CameraBounds not found in ACTIVE SCENE.");
            yield break;
        }

        // いったん外してから差し替えると安定
        confiner.m_BoundingShape2D = null;
        confiner.m_BoundingShape2D = shapes[0];
        confiner.m_Damping = confinerDamping;

        // 版差対策：画面端基準ON（存在する版だけ反映）
        var f = typeof(CinemachineConfiner2D).GetField("m_ConfineScreenEdges",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (f != null) f.SetValue(confiner, true);

        confiner.InvalidateCache();
        Debug.Log($"[VCam] Confiner set to: {shapes[0].gameObject.name} (active scene)");
    }
}
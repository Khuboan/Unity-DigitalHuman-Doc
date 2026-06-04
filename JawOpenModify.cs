using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class JawOpenModify : MonoBehaviour
{
    [Header("Source BlendShape")]
    [SerializeField] private SkinnedMeshRenderer faceRenderer;
    [SerializeField] private bool autoFindRenderer = true;
    [SerializeField] private string[] sourceBlendShapeNames = { "A25_Jaw_Open", "Jaw_Open", "jawOpen" };
    [SerializeField, Range(0f, 3f)] private float sourceScale = 1.0f;
    [SerializeField, Range(0f, 100f)] private float sourceMaxWeight = 70f;

    [Header("Jaw Bone")]
    [SerializeField] private Transform jawBone;
    [SerializeField] private bool autoFindJawBone = true;
    [SerializeField] private string[] jawBoneCandidateNames = { "CC_Base_JawRoot", "JawRoot", "Jaw", "CC_Base_Jaw", "jaw" };
    [SerializeField, Range(0f, 45f)] private float maxJawOpenAngle = 25f;
    [SerializeField] private bool useZAxisRotation = true;
    [SerializeField] private Vector3 jawOpenEuler = new Vector3(0f, 0f, -25f);

    [Header("Debug")]
    [SerializeField] private bool logMissingBindings = true;

    private Mesh cachedMesh;
    private int sourceBlendShapeIndex = -1;
    private Vector3 jawReferenceEuler;
    private bool jawReferenceCaptured;
    private bool missingSourceLogged;
    private bool missingJawLogged;

    private void OnEnable()
    {
        ResolveRenderer();
        ResolveJawBone();
        RefreshBindings();
        ApplyJawRotation();
    }

    private void OnDisable()
    {
        RemoveJawRotation();
    }

    private void OnValidate()
    {
        ResolveRenderer();
        ResolveJawBone();
        RefreshBindings();
        ApplyJawRotation();
    }

    private void LateUpdate()
    {
        ApplyJawRotation();
    }

    [ContextMenu("Refresh Bindings")]
    public void RefreshBindings()
    {
        if (!HasValidRenderer())
        {
            cachedMesh = null;
            sourceBlendShapeIndex = -1;
            return;
        }

        Mesh mesh = faceRenderer.sharedMesh;
        if (mesh != cachedMesh)
        {
            ResetJawToReference();
            cachedMesh = mesh;
        }

        sourceBlendShapeIndex = FindFirstBlendShapeIndex(mesh, sourceBlendShapeNames);
        if (sourceBlendShapeIndex < 0 && logMissingBindings && !missingSourceLogged)
        {
            Debug.LogWarning($"[{nameof(JawOpenModify)}] None of the source BlendShapes were found on '{mesh.name}': {JoinNames(sourceBlendShapeNames)}.", this);
            missingSourceLogged = true;
        }
        else if (sourceBlendShapeIndex >= 0)
        {
            missingSourceLogged = false;
        }

        if (jawBone == null && logMissingBindings && !missingJawLogged)
        {
            Debug.LogWarning($"[{nameof(JawOpenModify)}] Jaw bone was not found automatically. Please assign it manually.", this);
            missingJawLogged = true;
        }
        else if (jawBone != null)
        {
            missingJawLogged = false;
        }

        CaptureJawReference();
    }

    [ContextMenu("Auto Find Jaw Bone")]
    public void ResolveJawBone()
    {
        if (jawBone != null || !autoFindJawBone)
        {
            return;
        }

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform humanoidJaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
            if (humanoidJaw != null)
            {
                jawBone = humanoidJaw;
                return;
            }
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        jawBone = FindTransformByName(children, jawBoneCandidateNames, true);
        if (jawBone == null)
        {
            jawBone = FindTransformByName(children, jawBoneCandidateNames, false);
        }
    }

    [ContextMenu("Capture Jaw Reference")]
    public void CaptureJawReference()
    {
        if (jawBone == null)
        {
            jawReferenceCaptured = false;
            return;
        }

        jawReferenceEuler = jawBone.localEulerAngles;
        jawReferenceCaptured = true;
    }

    private void ResolveRenderer()
    {
        if (faceRenderer != null || !autoFindRenderer)
        {
            return;
        }

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer fallbackRenderer = null;
        int bestBlendShapeCount = -1;

        for (int i = 0; i < renderers.Length; i++)
        {
            Mesh mesh = renderers[i].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            if (FindFirstBlendShapeIndex(mesh, sourceBlendShapeNames) >= 0)
            {
                faceRenderer = renderers[i];
                return;
            }

            if (mesh.blendShapeCount > bestBlendShapeCount)
            {
                bestBlendShapeCount = mesh.blendShapeCount;
                fallbackRenderer = renderers[i];
            }
        }

        faceRenderer = fallbackRenderer;
    }

    private bool HasValidRenderer()
    {
        return faceRenderer != null && faceRenderer.sharedMesh != null;
    }

    private void ApplyJawRotation()
    {
        if (!HasValidRenderer() || sourceBlendShapeIndex < 0 || jawBone == null)
        {
            return;
        }

        if (!jawReferenceCaptured)
        {
            CaptureJawReference();
        }

        float sourceWeight = faceRenderer.GetBlendShapeWeight(sourceBlendShapeIndex);
        float normalizedWeight = Mathf.Clamp01((sourceWeight * sourceScale) / Mathf.Max(0.0001f, sourceMaxWeight));
        Vector3 nextEuler = jawReferenceEuler;

        if (useZAxisRotation)
        {
            nextEuler.z = NormalizeAngle(jawReferenceEuler.z) - (maxJawOpenAngle * normalizedWeight);
            jawBone.localEulerAngles = nextEuler;
        }
        else
        {
            nextEuler = jawReferenceEuler + (jawOpenEuler * normalizedWeight);
            jawBone.localRotation = Quaternion.Euler(nextEuler);
        }
    }

    private void RemoveJawRotation()
    {
        ResetJawToReference();
    }

    private static int FindFirstBlendShapeIndex(Mesh mesh, string[] candidateNames)
    {
        if (mesh == null || candidateNames == null)
        {
            return -1;
        }

        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidateName))
            {
                continue;
            }

            int index = mesh.GetBlendShapeIndex(candidateName);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static Transform FindTransformByName(Transform[] transforms, string[] candidateNames, bool exactMatch)
    {
        if (transforms == null || candidateNames == null)
        {
            return null;
        }

        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidateName))
            {
                continue;
            }

            for (int j = 0; j < transforms.Length; j++)
            {
                string transformName = transforms[j].name;
                if (exactMatch)
                {
                    if (transformName == candidateName)
                    {
                        return transforms[j];
                    }
                }
                else if (transformName.IndexOf(candidateName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return transforms[j];
                }
            }
        }

        return null;
    }

    private void ResetJawToReference()
    {
        if (jawBone == null || !jawReferenceCaptured)
        {
            return;
        }

        jawBone.localRotation = Quaternion.Euler(jawReferenceEuler);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private static string JoinNames(string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return "(none)";
        }

        return string.Join(", ", names);
    }
}

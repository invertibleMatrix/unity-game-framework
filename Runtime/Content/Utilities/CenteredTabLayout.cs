using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CenteredTabLayout : MonoBehaviour
{
    [Tooltip("The Scroll View's viewport RectTransform")]
    [SerializeField] private RectTransform viewport;
    
    private RectTransform content;
    private HorizontalLayoutGroup layoutGroup;
    private float lastViewportWidth = -1f;
    private float lastPreferredWidth = -1f;
    private bool needsEditorUpdate = false;
    
    #if UNITY_EDITOR
    private float lastEditorUpdateTime = 0f;
    private const float EDITOR_UPDATE_INTERVAL = 0.1f; // Update every 0.1s in edit mode
    #endif
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void OnEnable()
    {
        InitializeComponents();
        ResetCache();
        #if UNITY_EDITOR
        needsEditorUpdate = true;
        #endif
    }
    
    private void Start()
    {
        InitializeComponents();
    }
    
    private void OnValidate()
    {
        InitializeComponents();
        #if UNITY_EDITOR
        needsEditorUpdate = true;
        #endif
    }
    
    private void InitializeComponents()
    {
        if (content == null)
            content = GetComponent<RectTransform>();
        
        if (layoutGroup == null)
            layoutGroup = GetComponent<HorizontalLayoutGroup>();
    }
    
    private void ResetCache()
    {
        lastViewportWidth = -1f;
        lastPreferredWidth = -1f;
    }
    
    private void LateUpdate()
    {
        EnsureMinimumWidth();
    }
    
    private void EnsureMinimumWidth()
    {
        // Initialize components if null
        InitializeComponents();
        
        // Error checking
        if (content == null || layoutGroup == null)
            return;
        
        if (!IsValidForUpdate())
            return;
        
        // Get current dimensions
        float currentPreferredWidth = LayoutUtility.GetPreferredWidth(content);
        float currentViewportWidth = GetViewportWidth();
        
        // Skip if dimensions haven't changed (optimization)
        if (AlmostEqual(currentPreferredWidth, lastPreferredWidth) && 
            AlmostEqual(currentViewportWidth, lastViewportWidth))
        {
            return;
        }
        
        // Cache values
        lastPreferredWidth = currentPreferredWidth;
        lastViewportWidth = currentViewportWidth;
        
        // Calculate target width
        float paddingWidth = layoutGroup.padding.left + layoutGroup.padding.right;
        float minWidth = currentViewportWidth - paddingWidth;
        float targetWidth = Mathf.Max(currentPreferredWidth, minWidth);
        
        // Apply the width only if different
        if (!AlmostEqual(content.rect.width, targetWidth))
        {
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }
        
        #if UNITY_EDITOR
        // Force canvas update in edit mode to show changes immediately
        if (!Application.isPlaying)
        {
            ForceCanvasLayoutUpdate();
        }
        #endif
    }
    
    private bool IsValidForUpdate()
    {
        // Check if content is valid
        if (content == null)
            return false;
        
        // Check if viewport is set
        if (viewport == null)
            return false;
        
        // In edit mode, only update periodically to avoid spamming
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            float timeNow = Time.realtimeSinceStartup;
            if (timeNow - lastEditorUpdateTime < EDITOR_UPDATE_INTERVAL)
            {
                return false;
            }
            lastEditorUpdateTime = timeNow;
        }
        #endif
        
        return true;
    }
    
    private float GetViewportWidth()
    {
        if (viewport == null)
            return 0f;
        
        // Try to get width from rect first
        Rect viewportRect = viewport.rect;
        if (viewportRect.width > 0f)
            return viewportRect.width;
        
        // Fallback to sizeDelta
        if (viewport.sizeDelta.x > 0f)
            return viewport.sizeDelta.x;
        
        return 0f;
    }
    
    private bool AlmostEqual(float a, float b, float tolerance = 0.001f)
    {
        return Mathf.Abs(a - b) < tolerance;
    }
    
    #if UNITY_EDITOR
    private void ForceCanvasLayoutUpdate()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
    #endif
    
    /// <summary>
    /// Public method to force an immediate update
    /// </summary>
    public void ForceUpdate()
    {
        ResetCache();
        EnsureMinimumWidth();
    }
}
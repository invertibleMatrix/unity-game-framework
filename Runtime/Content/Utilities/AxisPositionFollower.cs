using UnityEngine;

[ExecuteAlways]
public class AxisPositionFollower : MonoBehaviour
{
    [Tooltip("The target RectTransform whose position will be copied")]
    [SerializeField] private RectTransform target;
    
    [Header("Axis Options")]
    [Tooltip("Copy the X position from target")]
    [SerializeField] private bool copyX = true;
    
    [Tooltip("Copy the Y position from target")]
    [SerializeField] private bool copyY = true;
    
    [Tooltip("Copy the Z position from target")]
    [SerializeField] private bool copyZ = true;
    
    [Header("Options")]
    [Tooltip("Update position in editor mode (when not playing)")]
    [SerializeField] private bool updateInEditMode = true;
    
    [Tooltip("Execute only when target is active")]
    [SerializeField] private bool onlyWhenTargetActive = false;
    
    /// <summary>
    /// Public access to the target for external scripts
    /// </summary>
    public RectTransform Target
    {
        get => target;
        set => target = value;
    }
    
    private RectTransform ownRectTransform;
    
    private void Awake()
    {
        ownRectTransform = GetComponent<RectTransform>();
        if (ownRectTransform == null)
        {
            Debug.LogWarning($"{nameof(AxisPositionFollower)}: No RectTransform found on {name}. This script should be used on UI elements.", this);
        }
    }
    
    private void LateUpdate()
    {
        // Skip if updating in edit mode is disabled
        if (!Application.isPlaying && !updateInEditMode)
            return;
        
        // Skip if we have no target
        if (target == null)
            return;
        
        // Skip if target should be active but isn't
        if (onlyWhenTargetActive && !target.gameObject.activeInHierarchy)
            return;
        
        ApplyPosition();
    }
    
    private void ApplyPosition()
    {
        if (target == null || ownRectTransform == null)
            return;
        
        Vector3 currentPos = ownRectTransform.position;
        Vector3 targetPos = target.position;
        
        // Copy only the enabled axes
        if (copyX)
            currentPos.x = targetPos.x;
        
        if (copyY)
            currentPos.y = targetPos.y;
        
        if (copyZ)
            currentPos.z = targetPos.z;
        
        // Apply the new position
        ownRectTransform.position = currentPos;
    }
    
    /// <summary>
    /// Manually update the position (useful for calling from other scripts)
    /// </summary>
    public void ForceUpdate()
    {
        if (target != null && ownRectTransform != null)
            ApplyPosition();
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Visual feedback in Scene view
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (target == null || ownRectTransform == null)
            return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(ownRectTransform.position, target.position);
        
        // Draw spheres at both positions
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ownRectTransform.position, 0.05f);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(target.position, 0.05f);
        
        UnityEditor.Handles.Label(
            ownRectTransform.position + Vector3.up * 0.1f,
            $"Following {(copyX ? "X" : "")}{(copyY ? "Y" : "")}{(copyZ ? "Z" : "")}"
        );
    }
    #endif
}
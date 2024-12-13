using UnityEngine;

public class DragableObject : MonoBehaviour
{
    public Transform target;
    [SerializeField]
    private bool isSelected;
    private Vector2 onMouseDownPosition;
    public bool isLockX;
    public bool isLockY;
    public void Awake()
    {
        if (target == null)
            target = transform;
    }
    public void OnMouseDown()
    {
        onMouseDownPosition = target.position;
        isSelected = true;
        GameManager.instance.dragingObject = gameObject;
    }
    public void OnMouseDrag()
    {
        if (isSelected)
        {
            var targetPosition = onMouseDownPosition + (GameManager.instance.onMouseDragPosition - GameManager.instance.onMouseDownPosition);
            target.position = new Vector3(isLockX ? target.position.x : targetPosition.x, isLockY ? target.position.y : targetPosition.y, target.position.z);
        }
    }
    public void OnMouseUp()
    {
        onMouseDownPosition = Vector2.zero;
        isSelected = false;
        GameManager.instance.dragingObject = null;
    }
}

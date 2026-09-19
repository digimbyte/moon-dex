using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gaskellgames.InputEventSystem
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class SelectionRect : GgMonoBehaviour
    {
        #region Variables

        [SerializeField, Required]
        [Tooltip("Reference to the RectTransform used for the selection rect visuals.")]
        private RectTransform rectTransform;
        
        [SerializeField, Required]
        [Tooltip("The user input used to trigger selection.")]
        private InputActionReference selectionInputReference;
        
        [SerializeField, Required]
        [Tooltip("The user input used to trigger selection.")]
        private InputActionReference mousePositionReference;
        
        /// <summary>
        /// Called when selectionInputReference is pressed and selectionRect is first started to be drawn.
        /// </summary>
        public GgEvent onSelectionStart;
        
        /// <summary>
        /// Called when selectionRect has been created and selectionInputReference is released.
        /// </summary>
        public GgEvent onSelectionEnd;

        private Camera cam;
        private bool isMouseDown;
        private bool isSelectionInProgress;
        private Vector2 mousePosition;
        private Vector2 selectionStartPosition;
        private List<SelectableObject> currentSelection;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Game loop

        private void OnEnable()
        {
            selectionInputReference.action.performed += SelectionStartCallback;
            selectionInputReference.action.canceled += SelectionEndCallback;
            
            // subscribe to input system update loop
            InputSystem.onAfterUpdate += InputSystem_OnAfterUpdate;

            // cache references
            cam = Camera.main;
            rectTransform.gameObject.SetActive(false);
            currentSelection = new List<SelectableObject>();
        }

        private void Start()
        {
            isMouseDown = false;
            isSelectionInProgress = false;

            // match references to screen x and y for mouse position
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
        }

        private void InputSystem_OnAfterUpdate()
        {
            UpdateAxisInput();
        }

        private void Update()
        {
            HandleSelection();
        }

        private void OnDisable()
        {
            currentSelection.Clear();
            
            selectionInputReference.action.performed -= SelectionStartCallback;
            selectionInputReference.action.canceled -= SelectionEndCallback;
            
            // unsubscribe from input system update loop
            InputSystem.onAfterUpdate -= InputSystem_OnAfterUpdate;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Methods

        private void SelectionStartCallback(InputAction.CallbackContext obj)
        {
            // cache state
            isMouseDown = true;
            selectionStartPosition = mousePosition;
            
            // clear selection
            for (int i = currentSelection.Count - 1; i >= 0; i--)
            {
                currentSelection[i].IsSelected = false;
                currentSelection.Remove(currentSelection[i]);
            }
            
            // notify listeners
            onSelectionStart?.Invoke();
        }

        private void SelectionEndCallback(InputAction.CallbackContext obj)
        {
            // cache state
            isMouseDown = false;
            isSelectionInProgress = false;
            rectTransform.gameObject.SetActive(false);
            
            // notify listeners
            onSelectionEnd?.Invoke();
        }

        private void UpdateAxisInput()
        {
            // cache mouse position
            mousePosition = mousePositionReference.action.ReadValue<Vector2>();
        }

        private void HandleSelection()
        {
            // checks
            if (!isMouseDown) { return; }
            if (Vector2.Distance(mousePosition, selectionStartPosition) <= 1) { return; }
            if (!isSelectionInProgress)
            {
                isSelectionInProgress = true;
                rectTransform.gameObject.SetActive(true);
            }
            if (!isSelectionInProgress) { return; }
            
            // update selection rect size and position
            float width = Mathf.Abs(mousePosition.x - selectionStartPosition.x);
            float height = Mathf.Abs(mousePosition.y - selectionStartPosition.y);
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = (mousePosition + selectionStartPosition) * 0.5f;
            
            // handle selection & deselection with AABB check
            float left = rectTransform.anchoredPosition.x - (rectTransform.sizeDelta.x * 0.5f);
            float right = rectTransform.anchoredPosition.x + (rectTransform.sizeDelta.x * 0.5f);
            float bottom = rectTransform.anchoredPosition.y - (rectTransform.sizeDelta.y * 0.5f);
            float top = rectTransform.anchoredPosition.y + (rectTransform.sizeDelta.y * 0.5f);
            foreach (SelectableObject selectableObject in SelectableManager.RegisteredObjects)
            {
                Vector2 screenPosition = cam.WorldToScreenPoint(selectableObject.transform.position);
                if (left < screenPosition.x && screenPosition.x < right && bottom < screenPosition.y && screenPosition.y < top)
                {
                    currentSelection.TryAdd(selectableObject);
                    selectableObject.IsSelected = true;
                }
                else if (currentSelection.Remove(selectableObject))
                {
                    selectableObject.IsSelected = false;
                }
            }
        }

        #endregion
        
    } // class end
}

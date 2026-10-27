using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Riftline
{
    public sealed class RiftlineUIPointer : MonoBehaviour
    {
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        RiftlineGame game;
        EventSystem pointerEventSystem;
        PointerEventData pointerData;
        GameObject hovered;
        GameObject pressed;
        GameObject clickTarget;
        GameObject dragTarget;
        LineRenderer line;
        Material lineMaterial;
        Transform cursor;
        Material cursorMaterial;
        bool selectWasHeld;

        public void Initialize(RiftlineGame owner)
        {
            if (game == owner && line) return;
            game = owner;
            if (line) return;
            var ray = new GameObject("Hand UI Ray");
            ray.transform.SetParent(transform, false);
            line = ray.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = .005f;
            line.endWidth = .002f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                lineMaterial = new Material(shader) { color = new Color(.12f, .85f, 1f) };
                line.sharedMaterial = lineMaterial;
            }
            line.enabled = false;
            BuildCursor();
        }

        void BuildCursor()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Gaze Cursor";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * .014f;
            Collider collider = go.GetComponent<Collider>();
            if (collider) Destroy(collider);
            cursor = go.transform;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                cursorMaterial = new Material(shader);
                renderer.sharedMaterial = cursorMaterial;
            }
            go.SetActive(false);
        }

        void PresentCursor(Vector3 point, bool onTarget)
        {
            if (!cursor) return;
            cursor.gameObject.SetActive(true);
            cursor.position = point;
            cursor.rotation = game.ViewCamera.transform.rotation;
            if (cursorMaterial)
                cursorMaterial.color = onTarget ? Color.white : new Color(.12f, .85f, 1f, .9f);
        }

        public void UpdatePointer(Pose pose, bool selectHeld, float scroll)
        {
            RiftlineUI ui = game ? game.GetComponentInChildren<RiftlineUI>() : null;
            if (!ui || !ui.Canvas || !EventSystem.current)
            {
                Cancel();
                return;
            }

            EnsurePointerData();
            Vector3 origin = pose.position;
            Vector3 direction = pose.rotation * Vector3.forward;
            var plane = new Plane(ui.Canvas.transform.forward, ui.Canvas.transform.position);
            if (!plane.Raycast(new Ray(origin, direction), out float distance) ||
                distance < 0f || distance > 4f)
            {
                Cancel();
                return;
            }

            Vector3 point = origin + direction * distance;
            line.enabled = true;
            // Start the beam slightly ahead of the eyes so gaze rays do not cross the face.
            line.SetPosition(0, origin + direction * .15f);
            line.SetPosition(1, point);
            Vector2 screen = game.ViewCamera.WorldToScreenPoint(point);
            pointerData.delta = screen - pointerData.position;
            pointerData.position = screen;
            hits.Clear();
            EventSystem.current.RaycastAll(pointerData, hits);
            GameObject hit = null;
            RaycastResult hitResult = default;
            foreach (RaycastResult candidate in hits)
            {
                if (candidate.gameObject.GetComponentInParent<Canvas>() != ui.Canvas) continue;
                hit = candidate.gameObject;
                hitResult = candidate;
                break;
            }

            pointerData.pointerCurrentRaycast = hitResult;
            PresentCursor(point, hit != null);
            if (hovered != hit)
            {
                if (hovered) ExecuteEvents.ExecuteHierarchy(hovered, pointerData, ExecuteEvents.pointerExitHandler);
                hovered = hit;
                pointerData.pointerEnter = hovered;
                if (hovered) ExecuteEvents.ExecuteHierarchy(hovered, pointerData, ExecuteEvents.pointerEnterHandler);
            }

            if (selectHeld && !selectWasHeld) Press();
            else if (!selectHeld && selectWasHeld) Release(true);
            if (selectHeld && dragTarget) Drag();
            selectWasHeld = selectHeld;

            if (hovered && Mathf.Abs(scroll) > .25f && !pointerData.dragging)
            {
                GameObject scrollTarget = ExecuteEvents.GetEventHandler<IScrollHandler>(hovered);
                if (scrollTarget)
                {
                    pointerData.scrollDelta = new Vector2(0f, scroll * Time.unscaledDeltaTime * 18f);
                    ExecuteEvents.Execute(scrollTarget, pointerData, ExecuteEvents.scrollHandler);
                    pointerData.scrollDelta = Vector2.zero;
                }
            }
        }

        void EnsurePointerData()
        {
            if (pointerData != null && pointerEventSystem == EventSystem.current) return;
            Cancel();
            pointerEventSystem = EventSystem.current;
            pointerData = new PointerEventData(pointerEventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -20,
                useDragThreshold = true
            };
        }

        void Press()
        {
            if (!hovered) return;
            pointerData.pressPosition = pointerData.position;
            pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
            pointerData.eligibleForClick = true;
            pointerData.dragging = false;
            pointerData.clickCount = 1;
            pointerData.clickTime = Time.unscaledTime;
            pressed = ExecuteEvents.ExecuteHierarchy(hovered, pointerData, ExecuteEvents.pointerDownHandler);
            clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hovered);
            if (!pressed) pressed = clickTarget;
            pointerData.pointerPress = pressed;
            pointerData.rawPointerPress = hovered;
            dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(hovered);
            pointerData.pointerDrag = dragTarget;
            if (dragTarget) ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.initializePotentialDrag);
        }

        void Drag()
        {
            if (!pointerData.dragging &&
                (!pointerData.useDragThreshold ||
                 (pointerData.position - pointerData.pressPosition).sqrMagnitude >=
                 EventSystem.current.pixelDragThreshold * EventSystem.current.pixelDragThreshold))
            {
                ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.beginDragHandler);
                pointerData.dragging = true;
                pointerData.eligibleForClick = false;
                if (pressed && pressed != dragTarget)
                {
                    ExecuteEvents.Execute(pressed, pointerData, ExecuteEvents.pointerUpHandler);
                    pressed = null;
                    pointerData.pointerPress = null;
                }
            }
            if (pointerData.dragging) ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.dragHandler);
        }

        void Release(bool allowClick)
        {
            if (pointerData == null) return;
            if (pressed) ExecuteEvents.Execute(pressed, pointerData, ExecuteEvents.pointerUpHandler);
            GameObject clicked = allowClick && pointerData.eligibleForClick && !pointerData.dragging &&
                hovered && clickTarget == ExecuteEvents.GetEventHandler<IPointerClickHandler>(hovered)
                    ? clickTarget
                    : null;
            if (dragTarget && pointerData.dragging)
                ExecuteEvents.Execute(dragTarget, pointerData, ExecuteEvents.endDragHandler);
            pressed = clickTarget = dragTarget = null;
            pointerData.pointerPress = pointerData.rawPointerPress = pointerData.pointerDrag = null;
            pointerData.dragging = pointerData.eligibleForClick = false;
            if (clicked)
            {
                ExecuteEvents.Execute(clicked, pointerData, ExecuteEvents.pointerClickHandler);
                game.PlayInputFeedback(FeedbackKind.Ui, game.ViewCamera.transform.position);
            }
        }

        public void Cancel()
        {
            if (line) line.enabled = false;
            if (cursor) cursor.gameObject.SetActive(false);
            Release(false);
            if (hovered && pointerData != null)
                ExecuteEvents.ExecuteHierarchy(hovered, pointerData, ExecuteEvents.pointerExitHandler);
            hovered = null;
            if (pointerData != null) pointerData.pointerEnter = null;
            selectWasHeld = false;
        }

        void OnDestroy()
        {
            Cancel();
            if (lineMaterial) Destroy(lineMaterial);
            if (cursorMaterial) Destroy(cursorMaterial);
            if (cursor) Destroy(cursor.gameObject);
        }
    }
}

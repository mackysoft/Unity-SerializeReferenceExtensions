using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Route the popup's real right-click to Unity's bound Foldout. Unity retains ownership
    // of the complete property menu, including disabled fields and third-party additions.
    internal sealed class FoldoutPropertyMenuManipulator : Manipulator
    {
        private readonly Toggle header;
        private readonly Func<bool> hasCurrentProperty;
        private VisualElement panelRoot;
        private EventBase pendingEvent;
        private bool sending;
        private bool dispatched;

        internal FoldoutPropertyMenuManipulator (Toggle header, Func<bool> hasCurrentProperty)
        {
            this.header = header;
            this.hasCurrentProperty = hasCurrentProperty;
        }

        protected override void RegisterCallbacksOnTarget ()
        {
            target.RegisterCallback<AttachToPanelEvent>(OnAttach);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            if (target.panel != null)
            {
                Connect();
            }
        }

        protected override void UnregisterCallbacksFromTarget ()
        {
            Disconnect();
            target.UnregisterCallback<AttachToPanelEvent>(OnAttach);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach (AttachToPanelEvent evt) => Connect();
        private void OnDetach (DetachFromPanelEvent evt) => Disconnect();

        private void Connect ()
        {
            Disconnect();
            // An enabled panel ancestor also receives events aimed at disabled inputs.
            panelRoot = target.panel.visualTree;
            RegisterMenuEvents();
        }

        private void Disconnect ()
        {
            CancelPendingEvent();
            if (panelRoot != null)
            {
                UnregisterMenuEvents();
                panelRoot = null;
            }
        }

        private bool IsMenuTarget (Vector2 position)
        {
            if (sending || target.panel == null || header.panel != target.panel
                || !target.worldBound.Contains(position) || !hasCurrentProperty())
            {
                return false;
            }
            // Bounds alone would also intercept an unrelated overlay above this input.
            // Pick can return the previous element cached at the same pointer position
            // after rebinding moves the header. Ask Unity for a fresh hit instead.
            VisualElement picked = target.panel.PickAll(position, null);
            return picked != null && (picked == target || target.Contains(picked));
        }

        private void Forward (EventBase original, EventBase copy)
        {
            CancelPendingEvent();
            // SendEvent queues while another event is being dispatched. Keep the pooled
            // copy owned until it reaches this callback, so it cannot be pooled and reused.
            pendingEvent = copy;
            dispatched = false;
            copy.target = header;
            original.PreventDefault();
            original.StopPropagation();
            sending = true;
            try
            {
                header.SendEvent(copy);
            }
            finally
            {
                sending = false;
                if (dispatched)
                {
                    ReleasePendingEvent();
                }
            }
        }

        private bool ConsumeForwardedEvent (EventBase evt)
        {
            if (evt != pendingEvent)
            {
                return false;
            }
            if (sending)
            {
                // Synchronous dispatch still needs the sender's reference until SendEvent returns.
                dispatched = true;
            }
            else
            {
                // A queued dispatch retains its own reference through the remaining callbacks.
                ReleasePendingEvent();
            }
            return true;
        }

        private void CancelPendingEvent ()
        {
            if (pendingEvent != null)
            {
                pendingEvent.StopImmediatePropagation();
                if (sending)
                {
                    dispatched = true;
                }
                else
                {
                    ReleasePendingEvent();
                }
            }
        }

        private void ReleasePendingEvent ()
        {
            pendingEvent?.Dispose();
            pendingEvent = null;
        }

#if UNITY_2022_2_OR_NEWER
        private void RegisterMenuEvents ()
        {
            panelRoot.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            panelRoot.RegisterCallback<ContextClickEvent>(OnContextClick, TrickleDown.TrickleDown);
        }

        private void UnregisterMenuEvents ()
        {
            panelRoot.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            panelRoot.UnregisterCallback<ContextClickEvent>(OnContextClick, TrickleDown.TrickleDown);
        }

        private void OnPointerUp (PointerUpEvent evt)
        {
            if (!ConsumeForwardedEvent(evt) && evt.button == (int)MouseButton.RightMouse && IsMenuTarget(evt.position))
            {
                Forward(evt, PointerUpEvent.GetPooled((IPointerEvent)evt));
            }
        }

        private void OnContextClick (ContextClickEvent evt)
        {
            if (IsMenuTarget(evt.mousePosition))
            {
                evt.StopImmediatePropagation();
            }
        }
#else
        private void RegisterMenuEvents () => panelRoot.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
        private void UnregisterMenuEvents () => panelRoot.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);

        private void OnMouseUp (MouseUpEvent evt)
        {
            if (!ConsumeForwardedEvent(evt) && evt.button == (int)MouseButton.RightMouse && IsMenuTarget(evt.mousePosition))
            {
                Forward(evt, MouseUpEvent.GetPooled((IMouseEvent)evt));
            }
        }
#endif
    }
}

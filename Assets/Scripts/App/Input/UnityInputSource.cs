using Hollow.Core.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hollow.App.Input
{
    /// <summary>
    /// Reads keyboard and mouse through the Input System (actions defined in code; rebinding is out of scope)
    /// and produces one <see cref="RawInputSample"/> per rendered frame.
    /// </summary>
    public sealed class UnityInputSource : System.IDisposable
    {
        readonly InputAction move;
        readonly InputAction fire;
        readonly InputAction cancel;
        readonly InputAction dash;
        readonly InputAction reload;
        readonly InputAction weapon1;
        readonly InputAction weapon2;
        readonly InputAction scroll;
        readonly InputAction equipItem;
        readonly InputAction cycleItem;
        readonly InputAction interact;

        public readonly InputAction Pause;
        public readonly InputAction Confirm;
        public readonly InputAction BalancePanel;
        public readonly InputAction DevPanel;

        public UnityInputSource()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            fire = Button("Fire", "<Mouse>/leftButton");
            cancel = Button("Cancel", "<Mouse>/rightButton");
            dash = Button("Dash", "<Keyboard>/space");
            reload = Button("Reload", "<Keyboard>/r");
            weapon1 = Button("Weapon1", "<Keyboard>/1");
            weapon2 = Button("Weapon2", "<Keyboard>/2");
            scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
            equipItem = Button("EquipItem", "<Keyboard>/f");
            cycleItem = Button("CycleItem", "<Keyboard>/g");
            interact = Button("Interact", "<Keyboard>/e");
            Pause = Button("Pause", "<Keyboard>/escape");
            Confirm = Button("Confirm", "<Keyboard>/enter");
            BalancePanel = Button("BalancePanel", "<Keyboard>/f9");
            DevPanel = Button("DevPanel", "<Keyboard>/f10");

            foreach (var a in All()) a.Enable();
        }

        readonly System.Collections.Generic.HashSet<InputAction> pressed = new();

        InputAction Button(string name, string binding)
        {
            var a = new InputAction(name, InputActionType.Button, binding);
            // Latch presses from callbacks so a press is never lost, whatever update it arrives in.
            a.performed += _ => pressed.Add(a);
            return a;
        }

        /// <summary>True once per physical press, until read.</summary>
        public bool TakePress(InputAction a) => pressed.Remove(a);

        InputAction[] All() => new[]
        {
            move, fire, cancel, dash, reload, weapon1, weapon2, scroll, equipItem, cycleItem, interact,
            Pause, Confirm, BalancePanel, DevPanel
        };

        public RawInputSample Read(Camera worldCamera)
        {
            var s = new RawInputSample
            {
                Move = move.ReadValue<Vector2>(),
                FireHeld = fire.IsPressed(),
                FireDown = TakePress(fire),
                CancelDown = TakePress(cancel),
                DashDown = TakePress(dash),
                ReloadDown = TakePress(reload),
                Weapon1Down = TakePress(weapon1),
                Weapon2Down = TakePress(weapon2),
                Scroll = scroll.ReadValue<float>(),
                EquipItemDown = TakePress(equipItem),
                CycleItemDown = TakePress(cycleItem),
                InteractDown = TakePress(interact),
                PointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(),
            };

            var mouse = Mouse.current;
            if (worldCamera != null && mouse != null)
            {
                Vector3 p = mouse.position.ReadValue();
                p.z = -worldCamera.transform.position.z;
                s.AimWorld = worldCamera.ScreenToWorldPoint(p);
            }
            return s;
        }

        public void Dispose()
        {
            foreach (var a in All()) a.Dispose();
        }
    }
}

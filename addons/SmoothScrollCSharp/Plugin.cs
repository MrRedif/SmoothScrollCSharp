#if TOOLS
using Godot;
using System;

namespace SmoothScroll
{
    [Tool]
    public partial class Plugin : EditorPlugin
    {
        public override void _EnterTree()
        {
            AddCustomType("ScrollDamper", "Resource",
                GD.Load<Script>("res://addons/SmoothScrollCSharp/scroll_damper/ScrollDamper.cs"),
                GD.Load<Texture2D>("res://addons/SmoothScrollCSharp/scroll_damper/icon.svg")
                );

            AddCustomType("SmoothScrollContainer", "ScrollContainer",
                GD.Load<Script>("res://addons/SmoothScrollCSharp/SmoothScrollContainer.cs"),
                GD.Load<Texture2D>("res://addons/SmoothScrollCSharp/class-icon.svg")
                );
        }

        public override void _ExitTree()
        {
            RemoveCustomType("ScrollDamper");
            RemoveCustomType("SmoothScrollContainer");
        }
    }

}
#endif
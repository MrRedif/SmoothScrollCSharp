using Godot;
using System;

[Tool]
[GlobalClass,Icon("res://addons/SmoothScrollCSharp/class-icon.svg")]
public partial class SmoothScrollContainer : ScrollContainer
{
    #region Export Variables
    [ExportGroup("Mouse Wheel")]
    // Drag impact for one scroll input.
    [Export(PropertyHint.Range, "0,10,0.01,or_greater,hide_slider")]
    public float speed = 1000.0f;

    /// <summary>
    /// ScrollDamper for wheel scrolling.
    /// </summary>
    [Export] public ScrollDamper wheelScrollDamper = new ExpoScrollDamper();


    [ExportGroup("Dragging")]
    /// <summary>
    /// ScrollDamper for dragging.
    /// </summary>
    [Export] public ScrollDamper draggingScrollDamper = new ExpoScrollDamper();
    /// <summary>
    /// Allow dragging with mouse or not.
    /// </summary>
    [Export] public bool dragWithMouse = true;
    /// <summary>
    /// Allow dragging with touch or not.
    /// </summary>
    [Export] public bool dragWithTouch = true;


    [ExportGroup("Container")]
    /// <summary>
    /// Below this value, snap content to boundary.
    /// </summary>
    [Export] public float justSnapUnder = 0.4f;

    /// <summary>
    ///  Margin of the currently focused element.
    /// </summary>
    [Export(PropertyHint.Range, "0,50")] public float followFocusMargin = 20.0f;

    /// <summary>
    /// Makes the container scrollable vertically.
    /// </summary>
    [Export] public bool allowVericalScroll = true;

    /// <summary>
    /// Makes the container scrollable horizontally
    /// </summary>
    [Export] public bool allowHorizontalScroll = true;

    /// <summary>
    /// Makes the container only scrollable where the content has overflow
    /// </summary>
    [Export] public bool autoAllowScroll = true;

    /// <summary>
    /// Whether the content of this container should be allowed to overshoot at the ends
    /// before interpolating back to its bounds
    /// </summary>
    [Export] public bool allowOverdragging = true;


    [ExportGroup("Scroll Bar")]
    bool _hideScrollbarOverTime = false;
    /// <summary>
    /// Hides scrollbar as long as not hovered or interacted with.
    /// </summary>
    [Export]
    public bool HideScrollbarOverTime
    {
        get
        {
            return _hideScrollbarOverTime;
        }
        set
        {
            _hideScrollbarOverTime = SetHideScrollbarOverTime(value);
        }
    }

    /// <summary>
    /// Time after scrollbar starts to fade out when 'hideScrollbarOverTime' is true.
    /// </summary>
    [Export] public float scrollbarHideTime = 5.0f;

    /// <summary>
    /// Fadein time for scrollbar when 'hideScrollbarOverTime' is true.
    /// </summary>
    [Export] public float scrollbarFadeInTime = 0.2f;

    /// <summary>
    /// Fadeout time for scrollbar when 'hideScrollbarOverTime' is true.
    /// </summary>
    [Export] public float scrollbarFadeOutTime = 0.5f;

    [ExportGroup("Debug")]

    // Adds debug information.
    [Export] public bool debugMode = false;

    #endregion

    #region Fields and Properties
    /// <summary>
    /// Current velocity of the 'contentNode'.
    /// </summary>
    public Vector2 velocity = Vector2.Zero;

    /// <summary>
    /// Control node to move when scrolling.
    /// </summary>
    public Control contentNode;

    /// <summary>
    /// Current position of 'contentNode'.
    /// </summary>
    public Vector2 pos = Vector2.Zero;

    /// <summary>
    /// Current ScrollDamper to use, recording to last input type.
    /// </summary>
    public ScrollDamper scrollDamper;

    /// <summary>
    /// When true, contentNode's position is only set by dragging the h scroll bar.
    /// </summary>
    public bool hScrollbarDragging = false;

    /// <summary>
    /// When true, contentNode's position is only set by dragging the v scroll bar.
    /// </summary>
    public bool vScrollbarDragging = false;

    /// <summary>
    /// When ture, 'contentNode' follows drag position.
    /// </summary>
    public bool contentDragging = false;

    /// <summary>
    /// Timer for hiding scroll bar.
    /// </summary>
    public Timer scrollbarHideTimer = new Timer();

    /// <summary>
    /// Tween for hiding scroll bar.
    /// </summary>
    public Tween scrollbarHideTween;

    /// <summary>
    /// Tween for scroll x to.
    /// </summary>
    public Tween scrollXToTween;

    /// <summary>
    /// Tween for scroll y to.
    /// </summary>
    public Tween scrollYToTween;

    /// <summary>
    /// [0,1] Mouse or touch's relative movement accumulation when overdrag[br]
    /// [2,3] Position where dragging starts[br]
    /// [4,5,6,7] Left_distance, right_distance, top_distance, bottom_distance
    /// </summary>
    public float[] dragTempData;

    /// <summary>
    /// Whether touch point is in deadzone.
    /// </summary>
    public bool isInDeadzone = false;

    bool _isScrolling = false;
    /// <summary>
    /// If content is beign scrolled.
    /// </summary>
    public bool IsScrolling
    {
        get
        {
            return _isScrolling;
        }
        set
        {
            if (_isScrolling != value)
            {
                if (value)
                {
                    EmitSignal(SignalName.ScrollStarted);
                }
                else
                {
                    EmitSignal(SignalName.ScrollEnded);
                }
            }
            _isScrolling = value;
        }
    }

    public enum SCROLL_TYPE { WHEEL, BAR, DRAG }
    /// <summary>
    /// Last type of input used to scroll.
    /// </summary>
    public SCROLL_TYPE lastScrollType;
    #endregion

    #region Constants

    public const string SCROLL_HORIZONTAL = "scroll_horizontal";
    public const string SCROLL_VERTICAL = "scroll_vertical";

    #endregion

    #region Overrides

    public override void _Ready()
    {
        if (debugMode)
            SetupDebugDrawing();

        //Initialize variables
        scrollDamper = wheelScrollDamper;

        GetVScrollBar().GuiInput += (@event) => ScrollbarInput(@event, true);
        GetHScrollBar().GuiInput += (@event) => ScrollbarInput(@event, false);
        GetViewport().GuiFocusChanged += OnFocusChanged;

        foreach (var c in GetChildren())
        {
            if (c is not ScrollBar)
            {
                contentNode = c as Control;
            }
        }

        AddChild(scrollbarHideTimer);
        scrollbarHideTimer.Timeout += ScrollbarHideTimerTimeout;
        if (HideScrollbarOverTime)
        {
            scrollbarHideTimer.Start(scrollbarHideTime);
        }
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;

        Scroll(true, velocity.Y, pos.Y, (float)delta);
        Scroll(false, velocity.X, pos.X, (float)delta);

        //Update vertical scroll bar.
        GetVScrollBar().SetValueNoSignal(-pos.Y);
        GetVScrollBar().QueueRedraw();

        //Update horizontal scroll bar.
        GetHScrollBar().SetValueNoSignal(-pos.X);
        GetHScrollBar().QueueRedraw();

        UpdateState();

        if (debugMode)
            QueueRedraw();
    }

    #region _gui_input
    public override void _GuiInput(InputEvent @event)
    {
        if (HideScrollbarOverTime)
        {
            ShowScrollbars();
            scrollbarHideTimer.Start(scrollbarHideTime);
        }

        //Mouse buttons
        if (@event is InputEventMouseButton eventMouse)
            switch (eventMouse.ButtonIndex)
            {
                case MouseButton.WheelDown:
                    if (eventMouse.IsPressed())
                    {
                        if (eventMouse.ShiftPressed || !(ShouldScrollVertical()))
                        {
                            if (ShouldScrollHorizontal())
                            {
                                velocity.X -= speed * eventMouse.Factor;
                            }
                        }
                        else if (ShouldScrollVertical())
                        {
                            velocity.Y -= speed * eventMouse.Factor;
                        }
                        ScrollGUIInputWheel();
                    }
                    break;
                case MouseButton.WheelUp:
                    if (eventMouse.IsPressed())
                    {
                        if (eventMouse.ShiftPressed || !(ShouldScrollVertical()))
                        {
                            if (ShouldScrollHorizontal())
                            {
                                velocity.X += speed * eventMouse.Factor;
                            }
                        }
                        else if (ShouldScrollVertical())
                        {
                            velocity.Y += speed * eventMouse.Factor;
                        }
                        ScrollGUIInputWheel();
                    }
                    break;
                case MouseButton.WheelLeft:
                    if (eventMouse.IsPressed())
                    {
                        if (eventMouse.ShiftPressed && ShouldScrollVertical())
                        {
                            velocity.Y -= speed * eventMouse.Factor;
                        }
                        else if (!eventMouse.ShiftPressed && ShouldScrollHorizontal())
                        {
                            velocity.X += speed * eventMouse.Factor;
                        }
                        ScrollGUIInputWheel();
                    }
                    break;
                case MouseButton.WheelRight:
                    if (eventMouse.IsPressed())
                    {
                        if (eventMouse.ShiftPressed && ShouldScrollVertical())
                        {
                            velocity.Y += speed * eventMouse.Factor;
                        }
                        else if (!eventMouse.ShiftPressed && ShouldScrollHorizontal())
                        {
                            velocity.X -= speed * eventMouse.Factor;
                        }
                        ScrollGUIInputWheel();
                    }
                    break;
                case MouseButton.Left:
                    if (eventMouse.IsPressed())
                    {
                        if (!dragWithMouse) return;

                        contentDragging = true;
                        isInDeadzone = true;
                        scrollDamper = draggingScrollDamper;
                        lastScrollType = SCROLL_TYPE.DRAG;
                        InitDragTempData();
                        KillScrollToTweens();
                    }
                    else
                    {
                        contentDragging = false;
                        isInDeadzone = false;
                    }
                    break;
            }

        //Drag
        if (@event is InputEventScreenDrag eventScreenDrag && dragWithTouch)
        {
            if (contentDragging)
            {
                if (ShouldScrollHorizontal())
                {
                    dragTempData[0] += eventScreenDrag.Relative.X;
                }
                if (ShouldScrollVertical())
                {
                    dragTempData[1] += eventScreenDrag.Relative.Y;
                }
                RemoveAllChildrenFocus(this);
                HandleContentDragging();
            }
        }
        else if (@event is InputEventMouseMotion eventMouseMotion && dragWithMouse)
        {
            if (contentDragging)
            {
                if (ShouldScrollHorizontal())
                {
                    dragTempData[0] += eventMouseMotion.Relative.X;
                }
                if (ShouldScrollVertical())
                {
                    dragTempData[1] += eventMouseMotion.Relative.Y;
                }
                RemoveAllChildrenFocus(this);
                HandleContentDragging();
            }
        }

        //Pan
        if (@event is InputEventPanGesture eventPan)
        {
            if (ShouldScrollHorizontal())
            {
                velocity.X = -eventPan.Delta.X * speed;
                KillScrollToTweens();
            }
            if (ShouldScrollVertical())
            {
                velocity.Y = -eventPan.Delta.Y * speed;
                KillScrollToTweens();
            }
        }

        //Screen Touch
        if (@event is InputEventScreenTouch eventScreenTouch)
        {
            if (eventScreenTouch.IsPressed())
            {
                if (!dragWithTouch) return;

                contentDragging = true;
                isInDeadzone = true;
                scrollDamper = draggingScrollDamper;
                lastScrollType = SCROLL_TYPE.DRAG;
                InitDragTempData();
                KillScrollToTweens();
            }
            else
            {
                contentDragging = false;
                isInDeadzone = false;
            }
        }

        //Handle Input
        GetTree().Root.SetInputAsHandled();
    }

    void ScrollGUIInputWheel()
    {
        lastScrollType = SCROLL_TYPE.WHEEL;
        scrollDamper = wheelScrollDamper;
        KillScrollToTweens();
    }
    #endregion

    public override void _Draw()
    {
        if (debugMode)
            DrawDebug();
    }

    public override Variant _Get(StringName property)
    {
        switch (property)
        {
            case SCROLL_HORIZONTAL:
                if (contentNode == null) return 0.0f;
                return -Convert.ToInt32(contentNode.Position.X);
            case SCROLL_VERTICAL:
                if (contentNode == null) return 0.0f;
                return -Convert.ToInt32(contentNode.Position.Y);
            default:
                return base._Get(property);

        }
    }

    public override bool _Set(StringName property, Variant value)
    {

        switch (property)
        {
            case SCROLL_HORIZONTAL:
                if (contentNode == null)
                {
                    ScrollHorizontal = 0;
                    return true;
                }
                ScrollHorizontal = (int)value;
                KillscrollXToTween();
                velocity.X = 0.0f;
                pos.X = Mathf.Clamp(-(float)value, -GetChildSizeXDiff(contentNode, true), 0.0f);
                return true;
            case SCROLL_VERTICAL:
                if (contentNode == null)
                {
                    ScrollVertical = 0;
                    return true;
                }
                ScrollVertical = (int)value;
                KillscrollYToTween();
                velocity.Y = 0.0f;
                pos.Y = Mathf.Clamp(-(float)value, -GetChildSizeYDiff(contentNode, true), 0.0f);
                return true;
            default:
                return base._Set(property, value);
        }
    }

    #endregion

    #region Signal Listeners

    #region _scrollbar_input
    void ScrollbarInput(InputEvent @event, bool vertical)
    {
        if (HideScrollbarOverTime)
        {
            ShowScrollbars();
            scrollbarHideTimer.Start(scrollbarHideTime);
        }

        //Mouse Input
        if (@event is InputEventMouseButton eventMouse)
        {
            if (eventMouse.ButtonIndex == MouseButton.WheelDown
                || eventMouse.ButtonIndex == MouseButton.WheelUp
                || eventMouse.ButtonIndex == MouseButton.WheelLeft
                || eventMouse.ButtonIndex == MouseButton.WheelRight)
            {
                _GuiInput(eventMouse);
            }

            if (eventMouse.ButtonIndex == MouseButton.Left)
            {
                ScrollBarInputPressCheck(vertical, eventMouse);
            }

        }

        //Touch Input
        if (@event is InputEventScreenTouch eventTouch)
        {
            ScrollBarInputPressCheck(vertical, eventTouch);
        }


    }

    void ScrollBarInputPressCheck(bool vertical, InputEvent @event)
    {
        if (@event.IsPressed())
        {
            if (vertical)
                vScrollbarDragging = true;
            else
                hScrollbarDragging = true;

            lastScrollType = SCROLL_TYPE.BAR;
            KillScrollToTweens();
        }
        else
        {
            if (vertical)
                vScrollbarDragging = false;
            else
                hScrollbarDragging = false;
        }
    }
    #endregion

    void OnFocusChanged(Control control)
    {
        if (FollowFocus)
        {
            EnsureControlVisible(control);
        }
    }

    void OnNodeAdded(Node node)
    {
        if (node is Control nodeControl && Engine.IsEditorHint())
        {
            if (IsAncestorOf(node))
            {
                nodeControl.MouseFilter = Control.MouseFilterEnum.Pass;
            }
        }
    }

    void ScrollbarHideTimerTimeout()
    {
        if (!AnyScrollbarDragged())
        {
            HideScrollbars();
        }
    }

    bool SetHideScrollbarOverTime(bool value)
    {
        if (!value)
        {
            scrollbarHideTimer?.Stop();
            scrollbarHideTween?.Kill();

            GetHScrollBar().Modulate = new Color(1f, 1f, 1f, 1f);
            GetVScrollBar().Modulate = new Color(1f, 1f, 1f, 1f);
        }
        else
        {
            if (scrollbarHideTimer.IsInsideTree())
            {
                scrollbarHideTimer?.Start(scrollbarHideTime);
            }
        }
        return value;
    }

    #endregion

    #region Logic

    void Scroll(bool vertical, float axisVelocity, float axisPos, float delta)
    {
        //If no scroll needed, don't apply forces.
        if (vertical && !ShouldScrollVertical()) return;
        else if (!vertical && !ShouldScrollHorizontal()) return;
        else if (scrollDamper == null) return;

        //Applies counterforces when overdragging.
        if (!contentDragging)
        {

            axisVelocity = HandleOverdrag(vertical, axisVelocity, axisPos, delta);

            //Move content node by applying velocity.
            var slideResult = scrollDamper.Slide(axisVelocity, delta);
            axisVelocity = slideResult[0];
            axisPos += slideResult[1];

            //Snap to boundary if close enough
            var snapResult = Snap(vertical, axisVelocity, axisPos);
            axisVelocity = snapResult[0];
            axisPos = snapResult[1];
        }
        else
        {
            axisVelocity = 0;
        }

        // If using scroll bar dragging, set the content_node's
        // position by using the scrollbar position.
        if (HandleScrollbarDrag()) return;

        if (vertical)
        {
            if (!allowOverdragging)
            {
                //Clamp if calculated position is beyond boundary.
                if (IsOutsideTopBoundary(axisPos))
                {
                    axisPos = 0;
                    axisVelocity = 0;
                }
                else if (IsOutsideBottomBoundary(axisPos))
                {
                    axisPos = -GetChildSizeYDiff(contentNode, true);
                    axisVelocity = 0;
                }
            }

            contentNode.Position = new Vector2(contentNode.Position.X, axisPos);
            pos.Y = axisPos;
            velocity.Y = axisVelocity;
        }
        else
        {
            if (!allowOverdragging)
            {
                //Clamp if calculated position is beyond boundary.
                if (IsOutsideTopBoundary(axisPos))
                {
                    axisPos = 0;
                    axisVelocity = 0;
                }
                else if (IsOutsideBottomBoundary(axisPos))
                {
                    axisPos = -GetChildSizeYDiff(contentNode, true);
                    axisVelocity = 0;
                }
            }

            contentNode.Position = new Vector2(axisPos, contentNode.Position.Y);
            pos.X = axisPos;
            velocity.X = axisVelocity;
        }

    }

    float HandleOverdrag(bool vertical, float axisVelocity, float axisPos, float delta)
    {
        if (scrollDamper == null) return 0.0f;

        //Calculate the size difference between this container and contentNode.
        var sizeDiff = vertical ? GetChildSizeYDiff(contentNode, true) : GetChildSizeXDiff(contentNode, true);

        //Calculate distance to left and right or top and bottom.
        var dist1 = vertical ? GetChildTopDist(axisPos, sizeDiff) : GetChildLeftDist(axisPos, sizeDiff);
        var dist2 = vertical ? GetChildBottomDist(axisPos, sizeDiff) : GetChildRightDist(axisPos, sizeDiff);

        //Calculate velocity to left and right or top and bottom.
        var targetVel1 = scrollDamper.CalculateVelocityToDest(dist1, 0.0f);
        var targetVel2 = scrollDamper.CalculateVelocityToDest(dist2, 0.0f);

        // Bounce when out of boundary. When velocity is not fast enough to go back, 
        // apply a opposite force and get a new velocity. If the new velocity is too fast, 
        // apply a velocity that makes it scroll back exactly.
        if (axisPos > 0.0f)
        {
            if (axisVelocity > targetVel1)
            {
                axisVelocity = scrollDamper.Attract(dist1, 0.0f, axisVelocity, delta);
            }
        }

        if (axisPos < -sizeDiff)
        {
            if (axisVelocity < targetVel2)
            {
                axisVelocity = scrollDamper.Attract(dist2, 0.0f, axisVelocity, delta);
            }
        }

        return axisVelocity;
    }

    //Snap to boundary if close enough in next frame.
    float[] Snap(bool vertical, float axisVelocity, float axisPos)
    {
        //Calculate the size difference between this container and contentNode.
        var sizeDiff = vertical ? GetChildSizeYDiff(contentNode, true) : GetChildSizeXDiff(contentNode, true);

        // Calculate distance to left and right or top and bottom.
        var dist1 = vertical ? GetChildTopDist(axisPos, sizeDiff) : GetChildLeftDist(axisPos, sizeDiff);
        var dist2 = vertical ? GetChildBottomDist(axisPos, sizeDiff) : GetChildRightDist(axisPos, sizeDiff);

        if (dist1 > 0.0
            && Mathf.Abs(dist1) < justSnapUnder
            && Mathf.Abs(axisVelocity) < justSnapUnder)
        {
            axisPos -= dist1;
            axisVelocity = 0.0f;
        }
        else if (dist2 < 0.0
            && Mathf.Abs(dist2) < justSnapUnder
            && Mathf.Abs(axisVelocity) < justSnapUnder)
        {
            axisPos -= dist2;
            axisVelocity = 0.0f;
        }

        return new float[] { axisVelocity, axisPos };
    }

    //Returns true when scrollbar was dragged.
    bool HandleScrollbarDrag()
    {
        if (hScrollbarDragging)
        {
            velocity.X = 0.0f;
            pos.X = -(float)GetHScrollBar().Value;
            return true;
        }

        if (vScrollbarDragging)
        {
            velocity.Y = 0.0f;
            pos.Y = -(float)GetVScrollBar().Value;
            return true;
        }

        return false;
    }

    void HandleContentDragging()
    {
        if (draggingScrollDamper == null) return;

        if (new Vector2(dragTempData[0], dragTempData[1]).Length() < ScrollDeadzone
            && isInDeadzone)
        {
            return;
        }
        else if (isInDeadzone)
        {
            isInDeadzone = false;
            dragTempData[0] = 0.0f;
            dragTempData[1] = 0.0f;
        }

        Func<float, float, float> CalculateDest = (delta, damping) =>
        {
            if (delta > 0.0f)
            {
                return delta / (1f + delta * damping * 0.00001f);
            }
            else
            {
                return delta;
            }
        };

        Func<float, float, float, float> CalculatePosition = (tempDist1, tempDist2, tempRelative) =>
        {
            if (tempRelative + tempDist1 > 0.0f)
            {
                var delta = Mathf.Min(tempRelative, tempRelative + tempDist1);
                var dest = CalculateDest(delta, draggingScrollDamper.AttractFactor);
                return dest - Mathf.Min(0.0f, tempDist1);
            }
            else if (tempRelative + tempDist2 < 0.0f)
            {
                var delta = Mathf.Max(tempRelative, tempRelative + tempDist2);
                var dest = -CalculateDest(-delta, draggingScrollDamper.AttractFactor);
                return dest - Mathf.Max(0.0f, tempDist2);
            }
            else
            {
                return tempRelative;
            }
        };

        if (ShouldScrollVertical())
        {
            var yPos = CalculatePosition(
                dragTempData[6], //Temp topDistance
                dragTempData[7], //Temp bottomDistance
                dragTempData[1]) //Temp y relative accumulation
                + dragTempData[3];
            velocity.Y = (yPos - pos.Y) / (float)GetProcessDeltaTime();
            pos.Y = yPos;
        }

        if (ShouldScrollHorizontal())
        {
            var xPos = CalculatePosition(
                dragTempData[4], //Temp leftDistance
                dragTempData[5], //Temp rightDistance
                dragTempData[0]) //Temp x relative accumulation
                + dragTempData[2];
            velocity.X = (xPos - pos.X) / (float)GetProcessDeltaTime();
            pos.X = xPos;
        }


    }

    void RemoveAllChildrenFocus(Node node)
    {
        if (node is Control control)
        {
            control.ReleaseFocus();
        }

        foreach (var child in node.GetChildren())
        {
            RemoveAllChildrenFocus(child);
        }
    }

    void UpdateState()
    {
        if ((contentDragging && !isInDeadzone)
            || AnyScrollbarDragged()
            || velocity != Vector2.Zero)
        {
            IsScrolling = true;
        }
        else
        {
            IsScrolling = false;
        }
    }

    void InitDragTempData()
    {
        //Calculate the size difference between this container and contentNode.
        var contentNodeSizeDiff = GetChildSizeDiff(contentNode, true, true);

        //Calculate distance to left, right, top and bottom.
        var contentNodeBoundaryDist =
            GetChildBoundaryDist(contentNode.Position, contentNodeSizeDiff);

        dragTempData = new float[]
        {
            0.0f,
            0.0f,
            contentNode.Position.X,
            contentNode.Position.Y,
            contentNodeBoundaryDist.X,
            contentNodeBoundaryDist.Y,
            contentNodeBoundaryDist.Z,
            contentNodeBoundaryDist.W
        };
    }

    /// <summary>
    /// Get container size x without v scroll bar 's width.
    /// </summary>
    /// <returns></returns>
    float GetSpareSizeX()
    {
        var sizeX = Size.X;

        if (GetVScrollBar().Visible)
            sizeX -= GetVScrollBar().Size.X;

        return Mathf.Max(sizeX, 0.0f);
    }

    /// <summary>
    /// Get container size y without h scroll bar 's height.
    /// </summary>
    /// <returns></returns>
    float GetSpareSizeY()
    {
        var sizeY = Size.Y;

        if (GetHScrollBar().Visible)
            sizeY -= GetHScrollBar().Size.Y;

        return Mathf.Max(sizeY, 0.0f);
    }

    /// <summary>
    /// Get container size without scroll bars' size.
    /// </summary>
    /// <returns></returns>
    Vector2 GetSpareSize()
    {
        return new Vector2(GetSpareSizeX(), GetSpareSizeY());
    }

    /// <summary>
    /// Calculate the size x difference between this container and child node.
    /// </summary>
    /// <param name="child"></param>
    /// <param name="clamp"></param>
    /// <returns></returns>
    float GetChildSizeXDiff(Control child, bool clamp)
    {
        var childSizeX = child.Size.X * child.Scale.X;
        var spareSizeX = GetSpareSizeX();

        //Falsify the size of the child node to avoid errors 
        //when its size is smaller than this container 's.
        if (clamp)
            childSizeX = Mathf.Max(childSizeX, spareSizeX);

        return childSizeX - spareSizeX;
    }

    /// <summary>
    /// Calculate the size y difference between this container and child node.
    /// </summary>
    /// <param name="child"></param>
    /// <param name="clamp"></param>
    /// <returns></returns>
    float GetChildSizeYDiff(Control child, bool clamp)
    {
        var childSizeY = child.Size.Y * child.Scale.Y;
        var spareSizeY = GetSpareSizeY();
        //Falsify the size of the child node to avoid errors 
        //when its size is smaller than this container 's.
        if (clamp)
            childSizeY = Mathf.Max(childSizeY, spareSizeY);

        return childSizeY - spareSizeY;
    }

    /// <summary>
    /// Calculate the size difference between this container and child node.
    /// </summary>
    /// <param name="child"></param>
    /// <param name="clampX"></param>
    /// <param name="clampY"></param>
    /// <returns></returns>
    Vector2 GetChildSizeDiff(Control child, bool clampX, bool clampY)
    {
        return new Vector2(
            GetChildSizeXDiff(child, clampX),
            GetChildSizeYDiff(child, clampY)
            );
    }

    /// <summary>
    /// Calculate distance to left.
    /// </summary>
    /// <param name="childPosX"></param>
    /// <param name="childSizeDiffX"></param>
    /// <returns></returns>
    float GetChildLeftDist(float childPosX, float childSizeDiffX)
    {
        return childPosX;
    }

    /// <summary>
    /// Calculate distance to right.
    /// </summary>
    /// <param name="childPosX"></param>
    /// <param name="childSizeDiffX"></param>
    /// <returns></returns>
    float GetChildRightDist(float childPosX, float childSizeDiffX)
    {
        return childPosX + childSizeDiffX;
    }

    /// <summary>
    /// Calculate distance to top.
    /// </summary>
    /// <param name="childPosY"></param>
    /// <param name="childSizeDiffY"></param>
    /// <returns></returns>
    float GetChildTopDist(float childPosY, float childSizeDiffY)
    {
        return childPosY;
    }

    /// <summary>
    /// Calculate distance to bottom.
    /// </summary>
    /// <param name="childPosY"></param>
    /// <param name="childSizeDiffY"></param>
    /// <returns></returns>
    float GetChildBottomDist(float childPosY, float childSizeDiffY)
    {
        return childPosY + childSizeDiffY;
    }

    /// <summary>
    /// Calculate distance to left, right, top and bottom.
    /// </summary>
    /// <param name="childPos"></param>
    /// <param name="childSizeDiff"></param>
    /// <returns></returns>
    Vector4 GetChildBoundaryDist(Vector2 childPos, Vector2 childSizeDiff)
    {
        return new Vector4(
            GetChildLeftDist(childPos.X, childSizeDiff.X),
            GetChildRightDist(childPos.X, childSizeDiff.X),
            GetChildTopDist(childPos.Y, childSizeDiff.Y),
            GetChildBottomDist(childPos.Y, childSizeDiff.Y)
            );
    }

    void KillscrollXToTween()
    {
        scrollXToTween?.Kill();
    }

    void KillscrollYToTween()
    {
        scrollYToTween?.Kill();
    }

    void KillScrollToTweens()
    {
        KillscrollXToTween();
        KillscrollYToTween();
    }

    #endregion

    #region Debug Drawing

    Gradient debugGradient = new Gradient();

    void SetupDebugDrawing()
    {
        debugGradient.SetColor(0, new Color(0f, 1f, 0f, 1f));
        debugGradient.SetColor(1, new Color(1f, 0f, 0f, 1f));
    }

    void DrawDebug()
    {
        //Calculate the size difference between this container and contentNode.
        var sizeDiff = GetChildSizeDiff(contentNode, false, false);

        //Calculate distance to left, right, top and bottom.
        var boundaryDist = GetChildBoundaryDist(contentNode.Position, sizeDiff);

        var bottomDistance = boundaryDist.W;
        var topDistance = boundaryDist.Z;
        var rightDistance = boundaryDist.Y;
        var leftDistance = boundaryDist.X;

        #region Overdrag Lines
        //Top, Bottom
        DrawLine(
            Vector2.Zero,
            new Vector2(0.0f, topDistance),
            debugGradient.Sample(Mathf.Clamp(topDistance / Size.Y, 0.0f, 1.0f)),
            5.0f
            );

        DrawLine(
            new Vector2(0.0f, Size.Y),
            new Vector2(0.0f, Size.Y + bottomDistance),
            debugGradient.Sample(Mathf.Clamp(-bottomDistance / Size.Y, 0.0f, 1.0f)),
            5.0f
            );

        //Left, Right
        DrawLine(
            new Vector2(0.0f, Size.Y),
            new Vector2(leftDistance, Size.Y),
            debugGradient.Sample(Mathf.Clamp(leftDistance / Size.Y, 0.0f, 1.0f)),
            5.0f
            );
        DrawLine(
            new Vector2(Size.X, Size.Y),
            new Vector2(Size.X + rightDistance, Size.Y),
            debugGradient.Sample(Mathf.Clamp(-rightDistance / Size.Y, 0.0f, 1.0f)),
            5.0f
            );
        #endregion

        #region Velocity Lines
        var origin = new Vector2(5.0f, Size.Y / 2.0f);

        DrawLine(
            origin,
            origin + new Vector2(0.0f, velocity.Y * 0.01f),
            debugGradient.Sample(Mathf.Clamp(velocity.Y * 2 / Size.Y, 0.0f, 1.0f)),
            5.0f
            );


        DrawLine(
            origin,
            origin + new Vector2(0.0f, velocity.X * 0.01f),
            debugGradient.Sample(Mathf.Clamp(velocity.X * 2 / Size.X, 0.0f, 1.0f)),
            5.0f
            );
        #endregion

    }


    #endregion

    #region API Functions

    /// <summary>
    /// Scrolls to specific x position.
    /// </summary>
    /// <param name="xPos"></param>
    /// <param name="duration"></param>
    public void ScrollXTo(float xPos, float duration = 0.5f)
    {
        if (!ShouldScrollHorizontal()) return;
        if (contentDragging) return;

        velocity.X = 0.0f;
        var sizeXDiff = GetChildSizeXDiff(contentNode, true);
        xPos = Mathf.Clamp(xPos, -sizeXDiff, 0.0f);
        KillscrollXToTween();
        scrollXToTween = CreateTween();
        var tweener = scrollXToTween.TweenProperty(this, "pos:x", xPos, duration);
        tweener.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quint);
    }

    /// <summary>
    /// Scrolls to specific y position.
    /// </summary>
    /// <param name="yPos"></param>
    /// <param name="duration"></param>
    public void ScrollYTo(float yPos, float duration = 0.5f)
    {
        if (!ShouldScrollVertical()) return;
        if (contentDragging) return;

        velocity.Y = 0.0f;
        var sizeDiffY = GetChildSizeYDiff(contentNode, true);
        yPos = Mathf.Clamp(yPos, -sizeDiffY, 0.0f);
        KillscrollYToTween();
        scrollYToTween = CreateTween();
        var tweener = scrollYToTween.TweenProperty(this, "pos:y", yPos, duration);
        tweener.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quint);
    }

    /// <summary>
    /// Scrolls up a page.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollPageUp(float duration = 0.5f)
    {
        var destination = contentNode.Position.Y + GetSpareSizeY();
        ScrollYTo(destination, duration);
    }

    /// <summary>
    /// Scrolls down a page.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollPageDown(float duration = 0.5f)
    {
        var destination = contentNode.Position.Y - GetSpareSizeY();
        ScrollYTo(destination, duration);
    }

    /// <summary>
    /// Scrolls left a page.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollPageLeft(float duration = 0.5f)
    {
        var destination = contentNode.Position.X + GetSpareSizeX();
        ScrollXTo(destination, duration);
    }

    /// <summary>
    /// Scrolls right a page.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollPageRight(float duration = 0.5f)
    {
        var destination = contentNode.Position.X - GetSpareSizeX();
        ScrollXTo(destination, duration);
    }

    /// <summary>
    /// Adds velocity to the vertical scroll.
    /// </summary>
    /// <param name="amount"></param>
    public void ScrollVertically(float amount)
    {
        velocity.Y -= amount;
    }

    /// <summary>
    /// Adds velocity to the horizontal scroll.
    /// </summary>
    /// <param name="amount"></param>
    public void ScrollHorizontally(float amount)
    {
        velocity.X -= amount;
    }

    /// <summary>
    /// Scrolls to top.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollToTop(float duration = 0.5f)
    {
        ScrollYTo(0.0f, duration);
    }

    /// <summary>
    /// Scrolls to bottom.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollToBottom(float duration = 0.5f)
    {
        ScrollYTo(GetSpareSizeY() - contentNode.Size.Y, duration);
    }

    /// <summary>
    /// Scrolls to left.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollToLeft(float duration = 0.5f)
    {
        ScrollXTo(0.0f, duration);
    }

    /// <summary>
    /// Scrolls to right.
    /// </summary>
    /// <param name="duration"></param>
    public void ScrollToRight(float duration = 0.5f)
    {
        ScrollXTo(GetSpareSizeX() - contentNode.Size.X, duration);
    }

    public bool IsOutsideTopBoundary(float yPos)
    {
        var sizeYDiff = GetChildSizeYDiff(contentNode, true);
        var topDist = GetChildTopDist(yPos, sizeYDiff);
        return topDist > 0.0f;
    }

    public bool IsOutsideTopBoundary()
    {
        return IsOutsideTopBoundary(pos.Y);
    }

    public bool IsOutsideBottomBoundary(float yPos)
    {
        var sizeYDiff = GetChildSizeYDiff(contentNode, true);
        var bottomDist = GetChildBottomDist(yPos, sizeYDiff);
        return bottomDist < 0.0f;
    }

    public bool IsOutsideBottomBoundary()
    {
        return IsOutsideTopBoundary(pos.Y);
    }

    public bool IsOutsideLeftBoundary(float xPos)
    {
        var xSizeDiff = GetChildSizeXDiff(contentNode, true);
        var leftDist = GetChildLeftDist(xPos, xSizeDiff);
        return leftDist > 0.0f;
    }

    public bool IsOutsideLeftBoundary()
    {
        return IsOutsideLeftBoundary(pos.X);
    }

    public bool IsOutsideRightBoundary(float xPos)
    {
        var xSizeDiff = GetChildSizeXDiff(contentNode, true);
        var rightSize = GetChildRightDist(xPos, xSizeDiff);
        return rightSize < 0.0f;
    }

    public bool IsOutsideRightBoundary()
    {
        return IsOutsideRightBoundary(pos.X);
    }

    /// <summary>
    /// Returns true if any scroll bar is being dragged.
    /// </summary>
    /// <returns></returns>
    public bool AnyScrollbarDragged()
    {
        return hScrollbarDragging || vScrollbarDragging;
    }

    /// <summary>
    /// Returns true if there is enough content height to scroll.
    /// </summary>
    /// <returns></returns>
    public bool ShouldScrollVertical()
    {
        var disableScroll =
            !allowVericalScroll
            || (autoAllowScroll && GetChildSizeYDiff(contentNode, false) <= 0)
            || scrollDamper == null;

        if (disableScroll)
        {
            velocity.Y = 0.0f;
            return false;
        }
        else
        {
            return true;
        }

    }

    /// <summary>
    /// Returns true if there is enough content width to scroll.
    /// </summary>
    /// <returns></returns>
    public bool ShouldScrollHorizontal()
    {
        var disableScroll =
            !allowHorizontalScroll
            || (autoAllowScroll && GetChildSizeXDiff(contentNode, false) <= 0)
            || scrollDamper == null;

        if (disableScroll)
        {
            velocity.X = 0.0f;
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Fades out scrollbars within given time.
    /// </summary>
    /// <param name="time"></param>
    public void HideScrollbars(float time)
    {
        scrollbarHideTween?.Kill();

        scrollbarHideTween = CreateTween();
        scrollbarHideTween.SetParallel(true);
        scrollbarHideTween.TweenProperty(GetVScrollBar(), "modulate", new Color(1f, 1f, 1f, 0f), time);
        scrollbarHideTween.TweenProperty(GetHScrollBar(), "modulate", new Color(1f, 1f, 1f, 0f), time);
    }

    /// <summary>
    /// Fades out scrollbars.
    /// </summary>
    public void HideScrollbars()
    {
        HideScrollbars(scrollbarFadeOutTime);
    }

    /// <summary>
    /// Fades in scrollbars within given time.
    /// </summary>
    /// <param name="time"></param>
    public void ShowScrollbars(float time)
    {
        scrollbarHideTween?.Kill();

        scrollbarHideTween = CreateTween();
        scrollbarHideTween.SetParallel(true);
        scrollbarHideTween.TweenProperty(GetVScrollBar(), "modulate", new Color(1f, 1f, 1f, 1f), time);
        scrollbarHideTween.TweenProperty(GetHScrollBar(), "modulate", new Color(1f, 1f, 1f, 1f), time);
    }

    /// <summary>
    /// Fades in scrollbars.
    /// </summary>
    /// <param name="time"></param>
    public void ShowScrollbars()
    {
        ShowScrollbars(scrollbarFadeInTime);
    }

    public new void EnsureControlVisible(Control control)
    {
        if (contentNode == null) return;
        if (contentNode.IsAncestorOf(control)) return;
        if (scrollDamper == null) return;

        var sizeDiff =
            (control.GetGlobalRect().Size - GetGlobalRect().Size)
            / (GetGlobalRect().Size / Size);

        var boundaryDist =
            GetChildBoundaryDist(
                (control.GlobalPosition - GlobalPosition) / (GetGlobalRect().Size / Size), sizeDiff);

        var contentNodePosition = contentNode.Position;

        if (boundaryDist.X < 0 + followFocusMargin)
            ScrollXTo(contentNodePosition.X - boundaryDist.X + followFocusMargin);
        else if (boundaryDist.Y > 0 - followFocusMargin)
            ScrollXTo(contentNodePosition.X - boundaryDist.Y - followFocusMargin);

        if (boundaryDist.Z < 0 + followFocusMargin)
            ScrollYTo(contentNodePosition.Y - boundaryDist.Z + followFocusMargin);
        else if (boundaryDist.W > 0 - followFocusMargin)
            ScrollYTo(contentNodePosition.Y - boundaryDist.W - followFocusMargin);

    }
    #endregion

}

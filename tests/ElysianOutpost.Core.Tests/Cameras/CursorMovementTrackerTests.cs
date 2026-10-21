// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.Core.Tests.Cameras;

public sealed class CursorMovementTrackerTests
{
    private static readonly float2 _zero = new(0f, 0f);

    [Fact]
    public void Next_FirstFrame_GivesNoMovement()
    {
        var tracker = new CursorMovementTracker();

        Assert.Equal(_zero, tracker.Next(new double2(960, 540)));
    }

    [Fact]
    public void Next_GivesTheMovementOnEachAxisSinceThePreviousFrame()
    {
        var tracker = new CursorMovementTracker();
        tracker.Next(new double2(960, 540));

        Assert.Equal(new float2(10f, 0f), tracker.Next(new double2(970, 540)));
        Assert.Equal(new float2(0f, -4f), tracker.Next(new double2(970, 536)));
        Assert.Equal(new float2(-2.5f, 7f), tracker.Next(new double2(967.5, 543)));
        Assert.Equal(_zero, tracker.Next(new double2(967.5, 543)));
    }

    // The defect of #106: in fly mode, a smaller window puts the hidden pointer outside the window.
    // The X server moves the pointer into the window, and the virtual cursor of GLFW jumps from 960,540 to 399,299.
    // GLFW sends the cursor enter event just before the jump.
    [Fact]
    public void Next_AfterACursorEnterEvent_IgnoresTheJumpAndTakesTheNewPositionAsReference()
    {
        var tracker = new CursorMovementTracker();
        tracker.Next(new double2(960, 540));

        tracker.IgnoreNextMovement();

        Assert.Equal(_zero, tracker.Next(new double2(399, 299)));
        Assert.Equal(new float2(5f, -3f), tracker.Next(new double2(404, 296)));
        Assert.Equal(new float2(1f, 2f), tracker.Next(new double2(405, 298)));
    }

    // A trace of the app under Xvfb: the frame reads the cursor at 740,460 with the focus.
    // Then the window loses and gets the focus in the same event processing.
    // The X server sends back the old cursor moves of GLFW, and the cursor jumps to 1060,640.
    [Fact]
    public void Next_AfterAFocusEvent_IgnoresTheCursorMovesThatGlfwSendsBack()
    {
        var tracker = new CursorMovementTracker();
        tracker.Next(new double2(740, 460));

        tracker.IgnoreNextMovement();

        Assert.Equal(_zero, tracker.Next(new double2(1060, 640)));
        Assert.Equal(new float2(-3f, 0f), tracker.Next(new double2(1057, 640)));
    }

    [Fact]
    public void Next_AfterTwoEventsInOneFrame_IgnoresOnlyOneFrame()
    {
        var tracker = new CursorMovementTracker();
        tracker.Next(new double2(100, 100));

        tracker.IgnoreNextMovement();
        tracker.IgnoreNextMovement();

        Assert.Equal(_zero, tracker.Next(new double2(0, 0)));
        Assert.Equal(new float2(0f, 12f), tracker.Next(new double2(0, 12)));
    }

    [Fact]
    public void IgnoreNextMovement_BeforeTheFirstFrame_GivesNoMovementOnlyInTheFirstFrame()
    {
        var tracker = new CursorMovementTracker();

        tracker.IgnoreNextMovement();

        Assert.Equal(_zero, tracker.Next(new double2(300, 200)));
        Assert.Equal(new float2(-1f, 0f), tracker.Next(new double2(299, 200)));
    }

    [Fact]
    public void Reset_ThenNext_GivesNoMovement()
    {
        var tracker = new CursorMovementTracker();
        tracker.Next(new double2(100, 100));

        tracker.Reset();

        Assert.Equal(_zero, tracker.Next(new double2(500, 400)));
        Assert.Equal(new float2(-100f, 0f), tracker.Next(new double2(400, 400)));
    }

    // The frames that the GLFW library of BRUTAL gave under Xvfb, in the disabled cursor mode without raw motion.
    // The list does not include the frames that have no change and no event.
    // Each frame gives the cursor position and the focus check of the frame.
    // Entered is true if GLFW sent a cursor enter event in the frame. FocusEvent is true if GLFW sent a focus event in the frame.
    // Only four frames contain a real mouse movement, in X and Y: (+10, 0), (0, +10), (+25, -5) and (-7, +3).
    // The other cursor changes come from window events.
    [Fact]
    public void Next_RecordedWindowEvents_GivesOnlyTheRealMouseMovement()
    {
        Frame[] frames =
        [
            new(960, 540, Entered: false, FocusEvent: false, Focused: true),
            new(970, 540, Entered: false, FocusEvent: false, Focused: true),      // Mouse movement +10, 0.
            new(399, 299, Entered: true, FocusEvent: false, Focused: true),       // Smaller window (400x300): the pointer was outside.
            new(399, 299, Entered: false, FocusEvent: false, Focused: true),      // Larger window.
            new(399, 309, Entered: false, FocusEvent: false, Focused: true),      // Mouse movement 0, +10.
            new(0, 0, Entered: true, FocusEvent: false, Focused: true),           // A window of 1x1 pixels.
            new(0, 0, Entered: false, FocusEvent: false, Focused: true),          // Larger window.
            new(0, 0, Entered: true, FocusEvent: false, Focused: true),           // Window move: the pointer was outside.
            new(1279, 719, Entered: true, FocusEvent: false, Focused: true),      // Window move back.
            new(0, 0, Entered: true, FocusEvent: false, Focused: true),           // Move and move back in one request batch.
            new(0, 0, Entered: false, FocusEvent: true, Focused: false),          // Hidden window.
            new(960, 540, Entered: true, FocusEvent: false, Focused: false),      // Visible window, without the focus.
            new(960, 540, Entered: false, FocusEvent: true, Focused: true),       // The window gets the focus.
            new(1280, 720, Entered: false, FocusEvent: true, Focused: false),     // The focus goes to the root window.
            new(1600, 900, Entered: false, FocusEvent: true, Focused: true),      // The focus comes back.
            new(2240, 1260, Entered: false, FocusEvent: true, Focused: true),     // Focus to the root window and back, between two frames.
            new(2560, 1440, Entered: false, FocusEvent: true, Focused: false),    // The focus goes to the root window.
            new(2020, 1180, Entered: false, FocusEvent: false, Focused: false),   // Another client moves the pointer.
            new(2340, 1360, Entered: false, FocusEvent: true, Focused: true),     // The focus comes back.
            new(2365, 1355, Entered: false, FocusEvent: false, Focused: true),    // Mouse movement +25, -5.
            new(2365, 1355, Entered: false, FocusEvent: false, Focused: true),    // Smaller window, with the pointer inside.
            new(2358, 1358, Entered: false, FocusEvent: false, Focused: true),    // Mouse movement -7, +3.
        ];
        var tracker = new CursorMovementTracker();
        var total = new float2(0f, 0f);

        foreach (Frame frame in frames)
        {
            // As in FreeFlyCameraController: the events come in the poll, before the frame reads the cursor.
            if (frame.Entered || frame.FocusEvent)
            {
                tracker.IgnoreNextMovement();
            }

            if (!frame.Focused)
            {
                tracker.Reset();
                continue;
            }

            total += tracker.Next(new double2(frame.CursorX, frame.CursorY));
        }

        Assert.Equal(new float2(28f, 8f), total);
    }

    private readonly record struct Frame(double CursorX, double CursorY, bool Entered, bool FocusEvent, bool Focused);
}

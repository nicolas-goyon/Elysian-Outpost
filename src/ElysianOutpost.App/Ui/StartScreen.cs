// SPDX-License-Identifier: MPL-2.0

using Brutal.ImGuiApi;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.App.Rendering;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The start screen: the BRUTAL logo in the center of a dark window, before the world shows.
/// </summary>
/// <remarks>
/// <para>
/// Section 2.2 of the BRUTAL License v1.0 asks for the BRUTAL logo before the gameplay starts.
/// The logo must stay unaltered, except for the scale, and it must show long enough to be legible.
/// The start screen shows the logo for at least <see cref="MinimumDuration"/>, from the presentation of its first legible frame.
/// A frame is legible when the logo is at least <see cref="MinimumLegibleWidth"/> pixels wide.
/// Thus a minimized or very small window, or a slow first frame, does not shorten this time.
/// </para>
/// <para>
/// The logo file has a white word on a transparent background. Thus the background is dark.
/// During the start screen, the renderer clears the window to <see cref="BackgroundColorSrgb"/> and does not draw the world.
/// The world continues to build in the background.
/// </para>
/// </remarks>
internal sealed class StartScreen
{
    /// <summary>
    /// The minimum time of the logo on the screen.
    /// </summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The background color of the start screen, as sRGB values (#121212).
    /// </summary>
    public static readonly float4 BackgroundColorSrgb = new(0x12 / 255f, 0x12 / 255f, 0x12 / 255f, 1f);

    /// <summary>
    /// The minimum width of the logo, in pixels, for a frame that starts the time of the start screen.
    /// </summary>
    public const float MinimumLegibleWidth = 128;

    // The logo uses half of the window width, but not more than half of the window height.
    private const float MaxWidthFraction = 0.5f;
    private const float MaxHeightFraction = 0.5f;

    private readonly ImGuiTexture _logo;
    private readonly LogCategory _log;
    private TimeSpan? _firstPresentTime;
    private bool _legible;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartScreen"/> class. The start screen is active until <see cref="Update"/> ends it.
    /// </summary>
    /// <param name="logo">The texture of the BRUTAL logo.</param>
    /// <param name="log">The log category for the start and the end of the start screen.</param>
    public StartScreen(ImGuiTexture logo, LogCategory log)
    {
        _logo = logo;
        _log = log;
    }

    /// <summary>
    /// Gets a value that tells if the start screen is active. Then the app does not show the world, and the input does nothing.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Ends the start screen when the logo was on the screen for the minimum time. Call it at the start of each frame.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from the clock of the main loop.</param>
    /// <remarks>
    /// The first frame without the logo starts at least <see cref="MinimumDuration"/> after the first presentation of the logo.
    /// Its presentation comes later. Thus the logo is on the screen for at least the minimum time.
    /// </remarks>
    public void Update(TimeSpan frameStart)
    {
        if (IsActive && _firstPresentTime is TimeSpan firstPresent && frameStart - firstPresent >= MinimumDuration)
        {
            IsActive = false;
            _log.Info($"The start screen ends {(frameStart - firstPresent).TotalSeconds:F2} s after the first presentation of the BRUTAL logo.");
        }
    }

    /// <summary>
    /// Records that the renderer presented a frame. Call it after each presented frame.
    /// </summary>
    /// <param name="presentTime">The time after the presentation, from the clock of the main loop.</param>
    public void OnFramePresented(TimeSpan presentTime)
    {
        if (IsActive && _firstPresentTime is null && _legible)
        {
            _firstPresentTime = presentTime;
            _log.Info($"The start screen shows the BRUTAL logo for at least {MinimumDuration.TotalSeconds:F0} s.");
        }
    }

    /// <summary>
    /// Draws the logo in the center of the window, in the current ImGui frame. The logo keeps its aspect ratio.
    /// </summary>
    public void Draw()
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        float2 area = viewport.Size;
        float2 logoSize = _logo.Size;
        float scale = MathF.Min(area.X * MaxWidthFraction / logoSize.X, area.Y * MaxHeightFraction / logoSize.Y);

        // Whole pixels for the corners keep the edges of the logo sharp.
        float width = MathF.Round(logoSize.X * scale);
        float height = MathF.Round(logoSize.Y * scale);
        _legible = width >= MinimumLegibleWidth;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        float2 corner = viewport.Pos;
        var min = new float2(corner.X + MathF.Floor((area.X - width) / 2), corner.Y + MathF.Floor((area.Y - height) / 2));
        var max = new float2(min.X + width, min.Y + height);
        ImGui.GetBackgroundDrawList(viewport).AddImage(_logo.Reference, in min, in max);
    }
}

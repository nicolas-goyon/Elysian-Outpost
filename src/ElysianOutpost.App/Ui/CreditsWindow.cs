// SPDX-License-Identifier: MPL-2.0

using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace ElysianOutpost.App.Ui;

/// <summary>
/// The credits window: the licenses of the app, the BRUTAL copyright notice and the third-party components.
/// The F1 key and the "Credits" button of the debug overlay open it.
/// </summary>
/// <remarks>
/// Section 2.1 of the BRUTAL License v1.0 asks for the BRUTAL copyright notice in the credits screen.
/// The full license texts are in the folder "licenses" next to the program (see Licenses.targets).
/// </remarks>
internal sealed class CreditsWindow
{
    /// <summary>
    /// The BRUTAL copyright notice, as the BRUTAL License v1.0 gives it.
    /// </summary>
    public const string BrutalCopyright = "BRUTAL game development framework is Copyright (c) 2024 Ahwoo Limited.";

    private static readonly float2 _windowSize = new(640, 480);
    private static readonly float2? _windowPivot = new float2(0.5f, 0.5f);

    // The third-party components in the output of the app, with their licenses (SPDX identifiers).
    // The source of the values is the LICENSES folder of each NuGet package.
    private static readonly (string Component, string License)[] _thirdPartyComponents =
    [
        (".NET runtime and libraries (Microsoft)", "MIT"),
        ("CommunityToolkit.HighPerformance (.NET Foundation)", "MIT"),
        ("GLFW 3.5.1", "Zlib"),
        ("SDL_GameControllerDB (in GLFW)", "Zlib"),
        ("Wayland protocol descriptions (in GLFW, Linux)", "MIT, HPND-sell-variant"),
        ("Vulkan-Headers", "Apache-2.0"),
        ("Vulkan-Docs reference pages", "CC-BY-4.0"),
        ("Vulkan Memory Allocator", "MIT"),
        ("Dear ImGui", "MIT"),
        ("ImPlot", "MIT"),
        ("stb", "MIT OR Unlicense"),
        ("glad", "MIT"),
        ("khrplatform.h", "MIT"),
        ("Khronos OpenGL API Specification", "Apache-2.0"),
        ("ResX.NET", "MIT"),
        ("shaderc", "Apache-2.0"),
        ("glslang", "BSD-3-Clause, BSD-2-Clause, Apache-2.0, MIT, MIT-Khronos-old, GPL-3.0-or-later WITH Bison-exception-2.2, NVIDIA glslang preprocessor license"),
        ("SPIRV-Tools", "Apache-2.0"),
        ("SPIRV-Headers", "MIT-Khronos-old, MIT, JsonCpp license, CC-BY-4.0"),
    ];

    /// <summary>
    /// Gets or sets a value that tells if the window is open.
    /// </summary>
    public bool Open { get; set; }

    /// <summary>
    /// Draws the window in the current ImGui frame, if it is open. The close button of the window closes it.
    /// </summary>
    public void Draw()
    {
        if (!Open)
        {
            return;
        }

        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        float2 corner = viewport.Pos;
        float2 area = viewport.Size;
        var center = new float2(corner.X + (area.X / 2), corner.Y + (area.Y / 2));
        ImGui.SetNextWindowPos(in center, ImGuiCond.Appearing, in _windowPivot);
        ImGui.SetNextWindowSize(in _windowSize, ImGuiCond.Appearing);
        bool open = true;
        if (ImGui.Begin("Credits"u8, ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.SeparatorText("Elysian Outpost"u8);
            ImGui.TextWrapped("Elysian Outpost contributors, https://github.com/nicolas-goyon/Elysian-Outpost"u8);
            ImGui.BulletText("Code: Mozilla Public License 2.0 (MPL-2.0)."u8);
            ImGui.BulletText("Assets: Creative Commons Attribution 4.0 International (CC BY 4.0)."u8);
            ImGui.TextWrapped("The BRUTAL logo belongs to Ahwoo Limited. These two licenses do not apply to it."u8);

            ImGui.SeparatorText("BRUTAL"u8);
            ImGui.TextWrapped(BrutalCopyright);
            ImGui.TextWrapped("License: BRUTAL License v1.0."u8);

            ImGui.SeparatorText("Third-party components"u8);
            DrawComponentTable();

            ImGui.Spacing();
            ImGui.TextWrapped("The full license texts are in the folder \"licenses\" next to the program. The file NOTICE.txt is next to the program."u8);
            ImGui.TextDisabled("F1: show or hide this window"u8);
        }

        // ImGui needs `End` also when `Begin` returns false.
        ImGui.End();
        if (!open)
        {
            Open = false;
        }
    }

    private static void DrawComponentTable()
    {
        const ImGuiTableFlags Flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("##ThirdParty"u8, 2, Flags))
        {
            return;
        }

        ImGui.TableSetupColumn("Component"u8, ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("License"u8, ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableHeadersRow();
        foreach ((string component, string license) in _thirdPartyComponents)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextWrapped(component);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(license);
        }

        ImGui.EndTable();
    }
}

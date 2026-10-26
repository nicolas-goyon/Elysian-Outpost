# Disclamer
**The project is still in its early phase, don't expect anything to work.**

# Description

The project involves revamping a game similar to StoneHearth, with two primary enhancements. Firstly, we're introducing a novel pathfinding algorithm that retains previously calculated paths, akin to a pheromone trail. This approach aims to facilitate new calculations by utilizing existing, quicker paths. The algorithm is based on A-star, but the cost of each block change is contingent on the block type. For instance, a roadblock is favored over a dirt block, and frequently used blocks are also prioritized.
The second major enhancement is ensuring the game functions on computers without a GPU or, at most, an Intel UHD. The goal is to optimize the game for low-end computers.

The project moves from Unity to BRUTAL, a C# framework on .NET 10 and Vulkan. [ADR 0001](docs/adr/0001-move-from-unity-to-brutal.md) explains the decision.

# Test builds

The CI makes a Windows test build for each change of `develop` and for each pull request (#110). To get the last build of `develop`:

1. Click "Sign in" on GitHub. GitHub gives the artifacts only to a user with an account.
2. Open the runs of the [Test build](https://github.com/nicolas-goyon/Elysian-Outpost/actions/workflows/test-build.yml?query=branch%3Adevelop) workflow, and select the last run of `develop`.
3. In the "Artifacts" part of the run page, download `ElysianOutpost-win-x64-<commit>`. GitHub keeps each artifact for 14 days.
4. Extract the zip file to a folder. Then start `ElysianOutpost.App.exe`.

The build runs on Windows 10 or 11 (x64), with a GPU driver for Vulkan 1.3. The build contains the .NET runtime, thus you do not install .NET. The zip file also contains `NOTICE.txt`, and the license files of the components in the `licenses` folder.

# StoneHearth’s description

StoneHearth is a captivating, open-world game that combines elements of city-building, strategy, and role-playing games (RPG), set in a fantastical, procedurally generated universe. Developed by Radiant Entertainment, the game offers a unique and immersive experience, allowing players to explore, expand, and defend their own thriving settlements.

Key Features:

1. **Procedural Generation**: StoneHearth's expansive world is procedurally generated, offering diverse landscapes, biomes, and endless possibilities for exploration. Each new game presents a unique terrain to conquer, filled with mysterious dungeons, hidden treasures, and intriguing characters.
2. **City-Building**: As the founder of a new settlement, players are tasked with designing, constructing, and managing their own village or city. This includes assigning tasks to the inhabitants (called "Hearthlings"), gathering resources, and ensuring the overall well-being and growth of the settlement.
3. **Customization**: StoneHearth offers a wealth of customization options for both the Hearthlings and the settlements they inhabit. Players can tailor their Hearthlings' appearance, skills, and equipment, while also designing their own buildings, fortifications, and town layouts.
4. **Dynamic Ecosystem**: The game features a complex and interconnected ecosystem, with various flora and fauna that interact with one another and the environment. Players must learn to adapt to the different biomes and exploit their resources to ensure their settlement's survival.
5. **RPG Elements**: In addition to city-building, StoneHearth incorporates RPG elements, allowing players to embark on quests, engage in diplomacy with other factions, and develop their Hearthlings' skills and abilities. As players progress, they'll encounter a rich tapestry of characters, lore, and story-driven events.
6. **Combat and Defense**: To protect their settlement from the dangers of the wilderness, players must recruit and train a militia, construct defensive structures, and devise strategies to repel invaders. Real-time combat allows players to actively engage in battles or issue commands to their Hearthlings.
7. **Crafting and Trade**: A robust crafting system enables players to create a wide variety of items, from weapons and armor to furniture and decorations. Establishing trade routes and negotiating with neighboring settlements can further bolster a player's resources and economic standing.
8. **Modding and Community**: StoneHearth boasts a dedicated modding community that continually expands the game's content through custom scenarios, buildings, items, and more. The game's developers actively support modding, making it easy for players to create and share their own unique additions to the StoneHearth universe.
9. **Art and Music**: The game's charming, voxel-based graphics create a visually appealing and distinctive world, while the original soundtrack composed by Kurtis Seits complements the atmosphere and enhances the overall gaming experience.
10. **Accessibility and Replayability**: StoneHearth's intuitive controls, tutorial system, and adjustable difficulty settings make it welcoming to both newcomers and experienced gamers. The procedurally generated worlds and myriad customization options ensure that each playthrough offers a fresh and engaging experience.

# License

- Assets use the [Creative Commons Attribution 4.0 International](LICENSE-ASSETS) license (CC BY 4.0). An asset is a media file of the game: an image, a 3D model, a texture, a color palette, a sound or a music file.
  - Attribution: "Elysian Outpost contributors, https://github.com/nicolas-goyon/Elysian-Outpost".
  - Now, these files are assets:
    - the files in `Elysian-Outpost/Assets/Resources/` (texture atlas and chunk model),
    - `Elysian-Outpost/ChunkBase.obj` (an exported chunk model),
    - `data/palette.json` (the colors of the block types).
- All other files of the project use the [Mozilla Public License 2.0](LICENSE) (MPL-2.0): the code, the documents and the project files.
- Third-party files keep their own licenses. The two licenses above do not apply to them:
  - `Elysian-Outpost/Assets/TextMesh Pro/`: files of TextMesh Pro, by Unity Technologies (Unity Companion License). The font LiberationSans uses the SIL Open Font License 1.1. The EmojiOne sprites use the terms of EmojiOne (see `Sprites/EmojiOne Attribution.txt`).
  - `Elysian-Outpost/Assets/Scripts/Base/SystemPlugin/UniClipboard.cs`: a changed version of [UniClipboard](https://github.com/sanukin39/UniClipboard), by wataru.sanuki, under the MIT License.
  - `Elysian-Outpost/Assets/Scripts/Libs/VoxelMeshOptimizer/OptimizationAlgorithms/DisjointSet/DisjointSet.cs`: a changed version of the union-find code of [algs4](https://github.com/kevin-wayne/algs4), by Robert Sedgewick and Kevin Wayne, under the GPL-3.0-or-later ([license text](Elysian-Outpost/LICENSE-GPL-3.0.txt)).
  - `src/ElysianOutpost.App/Assets/Brutal/brutal-logo.png`: the BRUTAL logo, a trademark of Ahwoo Limited. The app shows it on its start screen, as section 2.2 of the BRUTAL License v1.0 asks.
- From M1, the game uses BRUTAL. Each build obeys the BRUTAL License v1.0 (#42):
  - The start screen shows the BRUTAL logo for at least 2 seconds, before the world.
  - The credits window (F1, or the "Credits" button of the debug overlay) shows the BRUTAL copyright notice and the third-party components.
  - The build output contains `NOTICE.txt` next to the program, and the folder `licenses` with the license files of each NuGet package and of this project.

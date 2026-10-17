# Contributing to Elysian Outpost

This file gives the rules for a contribution to Elysian Outpost: branches, releases, commits, pull requests, issues, labels, milestones and licenses.

Write issues, pull requests, commit messages and documents in English. Use ASD-STE100 Simplified Technical English (STE100). The style guide and the glossary are in [docs/STYLE.md](docs/STYLE.md).

## Branches

The repository uses Git Flow.

| Branch | Starts from | Merges into | Use |
|---|---|---|---|
| `main` | — | — | Releases only. Each release has a tag. |
| `develop` | `main` | — | The work for the next release. This is the default branch. |
| `feature/<issue>-<name>` | `develop` | `develop` | A feature, a document or a chore. |
| `bugfix/<issue>-<name>` | `develop` | `develop` | A fix that is not urgent. |
| `release/<version>` | `develop` | `main`, then `main` into `develop` | The preparation of a release. |
| `hotfix/<version>` | `main` | `main`, then `main` into `develop` | An urgent fix for a release. |
| `poc/<name>` | `develop` | Never | A test or a proof of concept. The quality rules of this file do not apply. |

1. Start each branch from the branch that the table gives.
2. Put the issue number in the name of a `feature/` or `bugfix/` branch. Example: `feature/28-contributing`.
3. Do not push commits directly to `main` or `develop`. Use a pull request.
4. Do not merge a `poc/` branch.

### Releases

1. Make one release at the end of each milestone. The first release is `v0.1.0`, at the end of M1.
2. Use semantic versions: `MAJOR.MINOR.PATCH`.
3. Create the branch `release/<version>` from `develop`.
4. Open a pull request from the release branch into `main`. Merge it with a merge commit.
5. Add the tag `v<version>` to the merge commit on `main`.
6. Open a pull request from `main` into `develop`. Merge it with a merge commit.

### Hotfixes

1. Create the branch `hotfix/<version>` from `main`. Use the next patch version.
2. Do steps 4 to 6 of the release steps with the hotfix branch.

## Commits

Commit messages follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).

The first line has this format: `type(#issue): Description`

1. Use a type from the table below.
2. Put the issue number in the scope. This project uses the scope for the issue, not for a part of the code.
3. Write the description in the imperative mood. Start it with a capital letter. Do not put a period at the end.
4. Write a maximum of 72 characters on the first line.
5. In the body, explain why you make the change. Write the body in STE100.
6. Make atomic commits. Each commit does one thing.
7. For a small typo fix without an issue, write `N/A` as the issue. Example: `docs(N/A): Fix a typo in the README`.
8. For a change that breaks compatibility, add `!` after the scope. Example: `feat(#70)!: Change the format of the save files`.

| Type | Use | Label |
|---|---|---|
| `feat` | A new behavior or capability. | `type: feature` |
| `fix` | A fix of a defect. | `type: bug` |
| `perf` | A change that makes the code faster or smaller. | `type: performance` |
| `refactor` | A change of the code structure, with no change of behavior. | `type: refactor` |
| `port` | Code that moves from the legacy project. | `type: port` |
| `test` | Tests only. | `type: test` |
| `docs` | Documents only. | `type: docs` |
| `chore` | Build, repository or tool work. | `type: chore` |
| `ci` | CI workflows only. | `type: chore` |

## Pull requests

1. Use an issue that exists, or open a new issue first. Each pull request solves one issue.
2. Use the commit format for the title: `type(#issue): Description`. Example: `feat(#39): Draw the chunk meshes`.
3. Target `develop`. Only the pull requests from `release/` and `hotfix/` branches target `main`.
4. Write `Fixes #<issue>` in the description. GitHub closes the issue when the pull request merges into the default branch.
5. Do one change. Do not add changes that the issue does not ask for.
6. A pull request of type `port` or `refactor` does not change behavior, except the changes that its issue lists.
7. A pull request of type `perf` gives the benchmark results before and after the change.
8. Each pull request passes the CI checks (#11).
9. Complete the checklist of the pull request template.
10. Merge each pull request with a merge commit. Do not squash and do not rebase. Then the commits stay atomic, and `main` and `develop` keep the same commits.

These pull requests have no issue:

- A typo fix uses `N/A` in the title, and has no `Fixes` line.
- A release pull request has the title `chore(release): v<version>`.
- A pull request from `main` into `develop` has the title `chore(release): Merge v<version> into develop`.

### Review

- The owner reviews and merges each pull request.
- Now, a pull request can merge without an approval. When a second contributor is active, each pull request needs one approval.

## Issues

1. Use an issue form: task, bug or epic. Each form gives the sections of the issue.
2. Give each issue one `type:` label, one `area:` label and one `priority:` label.
3. Put each issue in a milestone. An epic without a milestone is for later.
4. Split an epic into issues before the work starts.

### Labels

| Label | Meaning |
|---|---|
| `type: feature` | A new behavior or capability. |
| `type: bug` | Something does not work as specified. |
| `type: port` | Moves code from the legacy project. No change of behavior, except the changes that the issue lists. |
| `type: refactor` | Changes the code structure. No change of behavior. |
| `type: performance` | Makes the code faster or smaller. The pull request gives measurements before and after. |
| `type: test` | Adds or changes tests only. |
| `type: docs` | Documents only. No change of code behavior. |
| `type: chore` | Build, CI, repository or tooling work. |
| `type: research` | Design, study or decision. The result is a document or a decision. |
| `type: epic` | A group of tasks. Split it into issues before the work starts. |
| `area: core` | Voxel data, chunks, mesher, voxel ray. |
| `area: worldgen` | World and terrain generation. |
| `area: render` | Vulkan renderer and shaders. |
| `area: app` | Window, main loop, input, settings, audio. |
| `area: ui` | Debug overlay and game UI. |
| `area: pathfinding` | Navigation grid, A* search, pheromone layer. |
| `area: simulation` | Citizens, tasks, items, buildings, time. |
| `area: tooling` | Solution, CI, repository rules, documentation tools. |
| `priority: must` | Necessary for the milestone. |
| `priority: should` | Important for the milestone. Can move to the next milestone if necessary. |
| `priority: could` | Useful. The milestone can close without it. |
| `needs-decision` | The owner must decide before the work starts. |
| `good first issue` | A small task for a new contributor. |
| `help wanted` | The owner asks for help. |
| `question` | A question. Not a task. |
| `duplicate`, `invalid`, `wontfix`, `not planned` | Reasons to close an issue without work. |

### Milestones

| Milestone | Goal |
|---|---|
| M0 - Foundations | Rules and decisions before the port: license, contribution rules, templates, style guide. |
| M1 - Port to BRUTAL | The app runs on .NET 10 and BRUTAL, with the features of the legacy project. The app replaces the legacy project. |
| M2 - Fix and optimize | The core library has tests and no known defects, and it is fast. The app meets the low-end target. |
| M3 - Pheromone pathfinding | Citizens find paths with block costs and pheromone trails. Trails appear where citizens walk often. |
| M4 - Vertical slice | A first playable loop: citizens remove voxels, carry items and build. |
| M5 - First public build | A downloadable build with varied worlds, a visual style, settings and audio. |

## Licenses

- Assets use the CC BY 4.0 license (`LICENSE-ASSETS`). The glossary defines an asset.
- All other files of the project use the Mozilla Public License 2.0 (`LICENSE`).
- Third-party files keep their own licenses. The README lists them.

When you send a contribution, you agree that it uses these licenses.

1. Do not add code or assets from other parties without a compatible license. Keep their license notices.
2. Use Stonehearth only as a reference for the game design. Do not copy its code or its assets (#27).

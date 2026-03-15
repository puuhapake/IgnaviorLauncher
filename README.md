# The Ignavior Launcher

The *Ignavior Launcher* is a standalone download manager designed for distributing binary patches for privately-distributed Windows games, primarily those created with the Unity game engine.

The program retrieves full builds and incremental updates from a remote host and applies them using locally using binary diff patches generated with [xdelta3](https://github.com/jmacd/xdelta).

## What?

Distributing homemade video games — especially those not intended for public release — can be challenging, particularly when frequent updates are involved. Requiring users to download the full build for every update wastes both bandwidth and time. 

This launcher provides a simple user interface for both retrieving the base builds and all incremental binary patches. The primary benefit of patching is that only the data required to update from one version to another is shipped.

## How does it work?

The launcher connects to a remote host containing a manifest describing:
- all available releases
- the identifier of the latest version of each release
- the base builds of each release
- the incremental update patches between versions

The system also finds hosted changelogs. When the user selects a game to launch, the launcher fetches the remote manifest, determines whether the game has been installed, determines the version of the installed release, and downloads, extracts and applies the archives for the required build(s) and patch(es) as needed. 

The program is built as a WPF graphical user interface built on .NET 8.0 with a C# backend. The system is designed for internal use, but can easily be adapted for the distribution of any self-hosted files.

## What does the Ignavior Launcher NOT do?

This is not a particularly sophisticated system. Essentially, all it does is retrieve archive files from an external host, managing their extraction and application. The program may support game-specific data, such as settings or achievements. The system is not intended for identification or authentication of users, advanced package management or digital rights management.

## Future plans

- Manifest extensions: sizes, hashes and mirror hosts
- Secrets: Password protection for internal-sensitivity files, with robust secret preservation and retrieval
- Patcher: Implement the Python-based patcher that was used for testing
- xDelta: bundle the xdelta diff tool with the program
- Self-Updater: The launcher itself is added as an entry in the manifest, containing (sequential or absolute) patches and self-applying updates automatically while running
- Changelog: Display hosted markdown files in the correct WPF panel
- UI updates: ship font styling, and allow for more robust customization, fix bugs, procedural icons etc.
- Offline: manifest caching, and if connection fails, enable Play button with currently installed version, even without updates
- Downloads: Progress bars, pausing and resuming, etc.

# Installation

Currently, in the proof-of-concept version, there are no official releases. Clone the project and build it, if you want.

# For developers

## Adding a new game
1. Build the base version of the game
2. Add the files to a `.rar` archive
3. Upload the archive to the host and any relevant mirrors
4. Copy the direct download URL of the file(s)
5. Add an entry to `manifest.json`:
   - Choose a unique identifier string
   - Set `name` to the display name of the game
   - Set `latest` to an integer representing the current latest version (e.g. `1`)
   - Set the values for `base`, including `version` and the `url`.
6. Create a changelog markdown file for the version, and place it in `changelogs/{game_id}/{version_integer}.md` of the file host
7. Upload the updated manifest and changelog(s). Now, the launcher will show the new game.

## Adding an update to an existing game
1. Build the new version of the game
2. Generate patches using the provided patcher, or an external xdelta3 build
3. Archive the patch folder into a `.rar` archive
4. Upload the patch to the host
5. Update `manifest.json`:
   - Add a new entry to the `patches` array with definitions for `from` (the old version), `to` (the updated version) and `url`
   - Increment the `latest` field to the new version
6. Add a changelog markdown for the new version, and place it in `changelogs/{game_id}/{latest_version}.md`
7. Upload the updated manifest and changelog(s). Now, the launcher will retrieve the update(s) and apply them sequentially.

# License

License terms have not yet been finalized. 
All rights reserved.

## Liability

Neither the developers of the launcher nor the launcher itself holds any responsibility for the integrity, morality or legality of files distributed using the program.

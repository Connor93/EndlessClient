# Project Skills Summary: EndlessClient

This document outlines the core skills and knowledge areas required for effectively contributing to and maintaining the EndlessClient project.

### 1. Core Language & Frameworks
*   **C# (Modern .NET 8.0):** The project targets .NET 8.0, utilizing modern C# features such as records, pattern matching, and async/await.
*   **MonoGame / XNA:** This is the primary game framework. Essential concepts include:
    *   The Game Loop (`Update` vs `Draw`).
    *   `SpriteBatch` rendering and texture management.
    *   The **Content Pipeline (`.mgcb`)** for asset compilation.
    *   Input handling (Keyboard, Mouse, Gamepad).

### 2. Game Architecture & Logic
*   **Custom UI Frameworks:** Relies on **`XNAControls`** for the user interface. Requires understanding of event-driven UI components within a game loop.
*   **Dependency Injection (DI):** Heavy use of modular design and service registration (see `ManualRegistrar.cs` and `DependencyMaster.cs`).
*   **State Management:** Handling complex transitions between game states (e.g., Login -> Character Selection -> In-Game).

### 3. Networking & Protocols
*   **TCP/IP Socket Programming:** Connects to legacy servers using raw sockets.
*   **Custom Binary Protocols:** Proficiency in constructing, serializing, and deserializing custom byte-stream packets is critical.
*   **Packet Handling:** Architecture for defining packet families/actions and implementing handlers for incoming server messages.

### 4. Legacy Data & File Formats
*   **Binary File Parsing:** Handling proprietary legacy formats for game data:
    *   `.pub` for items, NPCs, and classes.
    *   `.emf` for map data.
*   **Reverse Engineering Context:** Respecting and replicating specific behaviors and limitations of the original legacy client.

### 5. Tooling & DevOps
*   **Build Systems:** Familiarity with `MSBuild`/`.csproj` structures and platform-specific build scripts (`build-release.sh`, `build-release.ps1`).
*   **Unit Testing:** Maintaining high coverage across `.Test` projects (e.g., `EOLib.Test`, `EOLib.IO.Test`).
*   **CI/CD:** Understanding the `azure-pipelines.yml` configuration for automated builds.

### 6. Specialized Components
*   **EOBot Scripting:** Knowledge of custom interpreter logic for bot scripting.
*   **Audio Middleware:** Integration with `managed-midi` and platform-specific audio servers like Fluidsynth.

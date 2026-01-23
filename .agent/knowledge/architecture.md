---
name: EndlessClient Architecture
description: High-level architectural overview of the EndlessClient (C# / MonoGame) project.
---

# EndlessClient Architecture Guide

This document outlines the core architectural patterns for the **EndlessClient** project. This is a specific implementation of an MMORPG client using **MonoGame** (XNA) and **EOLib**.

## 1. High-Level Design
The application is a **Client-Server** architecture where the Client is a "dumb terminal" for the most part. It visualizes state provided by the server.
- **Engine:** MonoGame (XNA 4.0 Refresh).
- **Language:** C# 12 / .NET 8+.
- **Dependency Injection:** Autofac.
- **Protocol:** Custom TCP protocol (EOServ compatible).

## 2. Core Loops & Entry Points
The application runs on the standard MonoGame Loop:
1.  **Initialize/LoadContent:** Bootstraps the DI Container (`EndlessClientModule.cs`) and loads PE-based graphics (`INativeGraphicsManager`).
2.  **Update (Logic):**
    - **Network Polling:** `EOClient` polls for incoming data.
    - **Game Logic:** `EOGame.Update()` triggers state updates.
    - **UI Logic:** `XNAControl.Update()` handles mouse clicks/hover states *before* game logic to allow UI "consumption" of inputs.
3.  **Draw (Render):**
    - **Layers:** World (Ground/Objects) -> Players/NPCs -> UI (HUD/Windows).
    - **Batching:** Uses `SpriteBatch` with deferred sorting.

## 3. Key Systems & Libraries

### A. The Nervous System: EOLib & Networking
**EOLib** is the core library shared between client and bots.
- **Packets:** Defined by `PacketFamily` and `PacketAction`.
- **Packet Handling:**
    - Handlers implement `IPacketHandler`.
    - They parse bytes using `EOReader`.
    - They update **Repositories** (e.g., `ICharacterRepository`, `IMapRepository`).
    - **Rule:** Handlers *never* touch the UI directly. They update state.

### B. The Visual System: XNAControls (UI)
The UI is built entirely in code (No XAML/XML).
- **Controls:** Inherit from `XNAControl`.
- **Windows:** Inherit from `XNADialog`.
- **Rendering:** All assets are loaded via `INativeGraphicsManager` using `GFXTypes` (not file paths).
- **Input:** Controls must override `HandleMouse` and return `true` if they consume the event.

### C. The Wiring: Dependency Injection (Autofac)
- **Module:** `EndlessClientModule` contains the main registrations.
- **Pattern:** Constructor Injection is mandatory.
- **Factories:** Use `Func<T>` or specific factories for dynamic object creation (e.g., spawning a damage counter).

## 4. The "Data Flow" (The Golden Path)
How a feature moves from Network to Screen:

1.  **Server** sends `WALK_PLAYER` packet.
2.  **PacketHandler** (`WalkHandler.cs`) catches it.
3.  **Repo Update:** Handler updates `ICharacterRepository` with new coordinates.
4.  **Game Loop:** The `CharacterRenderer` sees the new coords in the repo.
5.  **Render:** The renderer interpolates the sprite position based on `GameTime`.
6.  **Screen:** The player moves smoothly on the next `Draw()` frame.

## 5. Common Pitfalls (Agent Warnings)
- **Do NOT** use `Content.Load<Texture2D>`. Always use `INativeGraphicsManager`.
- **Do NOT** use `Thread.Sleep`. This kills the render loop.
- **Do NOT** put game logic in `Draw()`. Keep it in `Update()`.
- **Do NOT** hardcode strings. Use `IContentProvider` for localization support.
# Crazy Driver

A Unity driving game prototype focused on arcade-style vehicle gameplay, road construction, and level experimentation.

## Project

**Engine:** Unity 6.0.5f1  
**Render Pipeline:** Universal Render Pipeline (URP) 17.5.0  
**Language:** C#  
**Primary platform:** Desktop development / prototype

## Features & Systems

The project currently serves as a development sandbox for a driving game and includes:

- Player vehicle setup and reusable vehicle prefabs
- Modular road and level pieces
- Test-level environment for gameplay iteration
- Unity Input System for input handling
- Cinemachine for camera control
- Unity AI Navigation for navigation-related experiments
- Unity Splines for spline-based level and road workflows
- ProBuilder for in-editor level prototyping
- URP-based rendering
- Imported environment and stylized game assets

## Development Workflow

The project is currently organized around rapid gameplay and level-design iteration. A typical workflow is:

1. Build or modify road modules in `Assets/Prefabs/Roads/`.
2. Assemble and test environments in `Assets/Scenes/Test Lvl.unity`.
3. Iterate on the player vehicle prefab.
4. Tune camera behaviour with Cinemachine.
5. Prototype navigation and spline-based systems as needed.
6. Validate the result in Play Mode before committing changes.

## Repository Notes

This repository is a work in progress. The structure and gameplay systems may change significantly as the prototype evolves.

## Roadmap

Planned areas for continued development include:

- Core arcade driving mechanics
- Traffic and AI vehicles
- Expanded road and level generation
- Camera and chase behaviour improvements
- Gameplay objectives and progression
- UI and game flow
- Performance optimization
- Playable builds

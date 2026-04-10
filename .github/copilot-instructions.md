# GitHub Copilot Instructions for the "Research Tree (Continued)" Mod

## Mod Overview and Purpose

The "Research Tree (Continued)" mod is an enhanced continuation of the original mod by Fluffy, aimed at providing a more intuitive and feature-rich research tree for the game RimWorld. The mod replaces the vanilla research interface, offering an improved, automatically generated research tree that is designed to be more readable and efficient for players.

## Key Features and Systems

- **Research Infocard**: Accessible via right-click, providing detailed information about research projects.
- **Queue Management**: Easily add or move research to the front of the queue with Ctrl+Left-click. Drag-and-drop functionality for rearranging queued projects.
- **Progress Tracking**: View current progress values of unfinished research projects.
- **UI Enhancements**: 
  - Locked camera when in the research window.
  - Shift opens the original research window.
  - Mod options to customize tree generation timing and pause the game when the tree is open.
  - Scroll-wheel support while holding Ctrl and a scroll-bar for the queue.
- **Performance Improvements**: Caching of research nodes to reduce stuttering and improved visual rendering techniques.
- **Customization Options**: Selectable background colors and the ability to hide "Missing Meme" warnings.
- **Compatibility**: Supports various other mods like Biotech, Research Reinvented, and more, ensuring a seamless experience.

## Coding Patterns and Conventions

- **C# Static Classes**: Utilize static classes for extensions (e.g., `Building_ResearchBench_Extensions`, `Def_Extensions`).
- **Separation of Concerns**: Classes are organized to separate functionality (e.g., UI handling, research logic, compatibility adjustments).
- **Naming Conventions**: Follows PascalCasing for classes and methods, and camelCasing for local variables.

## XML Integration

- Research definitions are enhanced through XML files, which support mod compatibility and allow for modification of research requirements and categories.
- XML is used to define mod settings accessible in-game, allowing players to customize their experience.

## Harmony Patching

- **Harmony** is used extensively for patching existing methods to enhance or modify game behavior.
- Focus on safe patching practices, ensuring compatibility with other mods by only altering specific behaviors as needed.
- Example usage includes patching methods in the game's research logic to enable features like custom research queuing and progress display.

## Suggestions for Copilot

When using GitHub Copilot, keep these suggestions in mind:

- **Context Awareness**: Utilize Copilot's ability to suggest code based on your current scope or function context, especially useful when working with Harmony patches.
- **Consistent Style**: Encourage suggestions that adhere to established coding conventions and patterns for consistency across the mod codebase.
- **Experimentation**: Use Copilot for generating new ideas for UI components or enhancing existing features, like visual elements in the research tree UI.
- **Efficiency Improvements**: Leverage Copilot to explore potential optimizations in frequently executed algorithms, such as node rendering or queue management.

By following these guidelines and utilizing the rich set of features provided by the "Research Tree (Continued)" mod, you can effectively enhance the research experience in RimWorld and integrate seamlessly with the broader mod ecosystem.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The .github/copilot-instructions.md file is included in the solution under the .github solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution .github copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.

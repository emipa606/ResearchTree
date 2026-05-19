# GitHub Copilot Instructions for RimWorld: Research Tree (Continued) Mod

## Mod Overview and Purpose

The "Research Tree (Continued)" mod is an update of the original "Research Tree" mod by Fluffy. This mod enhances the in-game research system by providing an intuitive and visually appealing tree structure for research projects. It aims to improve accessibility and management of research tasks, offering a better interface for players to plan and progress through their research, ultimately enhancing gameplay experience.

## Key Features and Systems

- **Research Infocard**: Easily view detailed research project information by right-clicking.
- **Queue Management Enhancements**: Add or prioritize research projects in the queue using Ctrl+Left-click. Includes drag-and-drop reordering, scroll-wheel support with Ctrl, and a scroll-bar for the research queue.
- **Graphical Improvements**: Improved aesthetics and performance with options to cache research nodes, lock the camera, and selectable background colors.
- **Mod Options**: Control when the research tree is generated and whether the game pauses while the tree is open.
- **Compatibility**: Extensive support for popular mods including Biotech, Research Reinvented, Save Our Ship 2, and more.
- **Miscellaneous**: Options to hide "Missing Meme" warnings and block research from tech-limiting mods.

## Coding Patterns and Conventions

- Follow C# best practices and naming conventions for classes, methods, and properties.
- Use a modular approach for defining extensions, keeping methods related to a specific functionality in dedicated classes.
- Utilize static classes for utility functions, such as `Def_Extensions`, to avoid unnecessary instantiation.
- Ensure methods follow single responsibility principle, focusing each on a specific operation.

## XML Integration

- XML is used to define research projects, buildings, and nodes as needed by the game.
- Ensure synchronization between C# logic and XML definitions.
- Utilize XML parsing to dynamically update the research tree layout based on mod-specific requirements.

## Harmony Patching

- Utilize Harmony for runtime method patching to extend or alter game behavior.
- Apply patches in a non-destructive manner to ensure compatibility and stability.
- Focus on augmenting essential game processes, such as research queue handling or UI modifications, using Harmony patches.

## Suggestions for Copilot

- **Code Reusability**: Encourage Copilot to suggest reusable code blocks for operations such as tree rendering, node management, and UI drawing.
- **Performance Optimization**: Assist with identifying areas for caching or asynchronous processing to enhance mod performance.
- **Feature Expansion**: Propose new features or improvements based on player feedback or common modding patterns.
- **Compatibility Tips**: Suggest methods for integrating with other mods, focusing on non-invasive system extensions via Harmony patches.

## Conclusion

The "Research Tree (Continued)" mod is an essential enhancement for players seeking a more organized and efficient way to manage RimWorld's research mechanics. Its compatibility with various popular mods makes it a versatile choice for diverse mod setups. By following best practices and leveraging the capabilities of GitHub Copilot, developers can continue improving this mod and ensure it remains a valuable addition to the RimWorld modding community.

## Project Solution Guidelines
- Relevant mod XML files are included as Solution Items under the solution folder named XML, these can be read and modified from within the solution.
- Use these in-solution XML files as the primary files for reference and modification.
- The `.github/copilot-instructions.md` file is included in the solution under the `.github` solution folder, so it should be read/modified from within the solution instead of using paths outside the solution. Update this file once only, as it and the parent-path solution reference point to the same file in this workspace.
- When making functional changes in this mod, ensure the documented features stay in sync with implementation; use the in-solution `.github` copy as the primary file.
- In the solution is also a project called Assembly-CSharp, containing a read-only version of the decompiled game source, for reference and debugging purposes.
- For any new documentation, update this copilot-instructions.md file rather than creating separate documentation files.

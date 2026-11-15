# Project instructions

- This is a simple desktop application. Keep the architecture small and simple; do not introduce enterprise architecture or unnecessary abstractions.
- Git is the backup. Do not create artifacts, backup files, or separate backups.
- Always work on the `main` branch. Do not create feature branches or separate worktrees.
- Store all application configuration and data in the application directory itself. Resolve these paths relative to the executable directory, not the current working directory.
- Do not store configuration or data in OS-managed user directories such as AppData, LocalAppData, the user profile, or other per-user configuration/data folders.

## C# style and maintainability

- Do not use top-level statements in C#. Use explicit types and methods, including a `Program` class with a `Main` method for the application entry point.
- Format code for human readability with consistent indentation, clear names, and readable spacing. Do not compress code into dense statements.
- Keep source files small and focused. Treat 1,000 lines as the point to review and split a file, and 2,000 lines as the maximum, not a target or minimum.
- Redesign and split larger files into smaller, cohesive components with clear responsibilities. Keep the result modular and easy to maintain while preserving this project's simple architecture.

## FishGfx and FishUI

- We maintain both FishGfx and FishUI. Modify either library when needed.
- Commit and push library changes to their respective upstream repositories. Do not maintain local-only copies or patches.
- Update this project's submodule references to the pushed commits, including the nested FishUI reference when applicable.

## User input

- When calling `request_user_input`, never set or provide `autoResolutionMs`.
- Questions requiring user input must wait indefinitely for an explicit response. Never choose a recommended answer, default answer, or best guess because the user has not responded.

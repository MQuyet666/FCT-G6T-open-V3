---
name: infrastructure-layer
description: Use this skill when creating, modifying, or reviewing the FCT-G6T Infrastructure layer, including camera adapters, serial device adapters, JSON configuration providers, file logging, dependency injection wiring, and any C# adapter that implements Domain or Application interfaces for hardware, files, SDKs, or operating-system services.
---

# Infrastructure Layer

Use Infrastructure as the adapter layer between pure application contracts and external systems.

## Boundary

Infrastructure may import:
- `FCT.G6T.Domain.Interfaces`
- `FCT.G6T.Domain.Models`
- `FCT.G6T.Application.Interfaces`
- `FCT.G6T.HAL.*`
- Hardware, SDK, file, JSON, logging, and OS libraries

Infrastructure must not contain:
- WinForms UI logic
- Test workflow orchestration
- Business decisions that belong in Application
- Domain models coupled to SDK or OS types

Correct dependency flow:

```text
Presentation -> Application -> Domain interfaces
                           -> Infrastructure implementations -> HAL/SDK/OS/File
```

## Required Pattern

When adding a new Infrastructure capability:

1. Put the interface in `Domain/Interfaces` for hardware/domain-facing services, or `Application/Interfaces` for app-facing providers.
2. Implement the interface in `src/Infrastructure/{Module}`.
3. Keep one public class per `.cs` file.
4. Inject HAL wrappers, settings, and `ILogger<T>` through the constructor.
5. Register the implementation once in `Program.cs`.
6. Let Application call only the interface.

## Current Modules

- Camera: `SdkCameraAdapter`, `OpenCvCameraAdapter`
- Serial: `G6TAdapter`, `DetectorAdapter`, `QrCodeReaderAdapter`, `ComPortProvider`
- Configuration: settings classes and `JsonTestCaseProvider`
- Logging: `FileLogger`, `FileLoggerProvider`

## Reference Files

Read these only when working in the matching module:

- `references/camera-adapters.md`
- `references/serial-adapters.md`
- `references/configuration.md`
- `references/logging.md`
- `references/dependency-injection.md`

Use `references/adapter-template.cs` as the standard C# skeleton for a new Infrastructure adapter.

## Completion Checklist

- [ ] Namespace follows `FCT.G6T.Infrastructure.{Module}`
- [ ] Public class implements a Domain/Application interface
- [ ] No UI imports
- [ ] No direct business workflow decisions
- [ ] Hardware/file/SDK failures include useful device context
- [ ] Async I/O methods use `ConfigureAwait(false)`
- [ ] Disposable resources are cleaned up
- [ ] Implementation is registered in `Program.cs`

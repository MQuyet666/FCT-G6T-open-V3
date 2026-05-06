# Camera Adapters

Camera adapters implement `FCT.G6T.Domain.Interfaces.ICameraService`.

## Responsibilities

- Start and stop camera preview.
- Raise `FrameReady` with `FrameReadyEventArgs`.
- Return a snapshot through `CaptureFrameAsync`.
- Convert SDK/OpenCV frame data to `Bitmap`.
- Own and dispose camera resources.

## Existing Implementations

- `SdkCameraAdapter`: preferred production adapter using `DVPCamera`.
- `OpenCvCameraAdapter`: alternative adapter using `OpenCvSharp.VideoCapture`.

## Rules

- Do not update WinForms controls directly.
- Raise events only; Presentation decides how to marshal to UI thread.
- Keep frame buffers bounded.
- Dispose discarded `Bitmap` instances.
- Include camera index or SDK status in thrown errors.

# Logging

Infrastructure owns the file logger implementation.

## Existing Files

- `FileLogger`
- `FileLoggerProvider`

## Rules

- Use `Microsoft.Extensions.Logging`.
- Write daily rolling files: `{FilePrefix}{yyyy-MM-dd}.log`.
- Keep log cleanup inside provider initialization.
- Swallow cleanup deletion failures only; do not swallow write failures silently unless explicitly required.
- Keep log formatting stable because UI/support may depend on it.

# Development Guide

This document explains development workflows, coding conventions, and how to contribute.

## Coding conventions

- Use MVVM for UI logic; keep code-behind minimal
- Register services in `MauiProgram.cs` using dependency injection
- Use `Async` suffix for async methods

## Running tests

Add unit tests under a `tests/` project and run via `dotnet test`.

## Contribution

1. Fork the repo
2. Create a feature branch
3. Open a pull request with description and testing notes

## Notes

- Keep platform-specific code inside the `Platforms/` folder
- Use `SecureStorage` for sensitive data

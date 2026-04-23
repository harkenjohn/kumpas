# Kumpas

Kumpas is a communication platform built around sign-language-assisted conversation. This repository contains:

- A Unity mobile application for sign-to-speech and speech-to-sign interaction
- An ASP.NET Core admin portal for account, conversation, analytics, and reporting workflows

## Repository Structure

- `Assets/`, `Packages/`, `ProjectSettings/`: Unity project files
- `Assets/Scripts/`: app logic for authentication, chat, ASL recognition, speech, UI, and Supabase integration
- `Kumpas.AdminWeb/`: ASP.NET Core MVC admin portal
- `LocalPackages/MediaPipeUnityPlugin-all/`: local MediaPipe Unity package source used by the Unity project

## Main Features

### Unity Application

- User login, registration, profile management, password updates, and account deactivation
- Chat session creation and room-code-based joining
- Realtime messaging backed by Supabase
- Sign-to-speech flow using ASL recognition and Android text-to-speech
- Speech-to-sign flow with text messages that trigger the signing interface
- Conversation history and message playback inside the app

### Admin Portal

- Admin authentication with cookie-based sessions
- Dashboard and analytics views
- Account search, filtering, status toggling, password updates, and soft-delete workflows
- Conversation browsing, detail views, and deletion
- Reports and translation service monitoring pages
- PostgreSQL access through Entity Framework Core

## Tech Stack

- Unity `6000.2.10f1`
- C#
- AR Foundation, ARCore, ARKit, XR Interaction Toolkit
- MediaPipe Unity plugin
- Supabase Auth, Realtime, and Postgres
- ASP.NET Core MVC on `.NET 9`
- Entity Framework Core with PostgreSQL (`Npgsql`)

## Prerequisites

Install the following before working in this repository:

- Unity Editor `6000.2.10f1`
- .NET SDK `9.0`
- A PostgreSQL/Supabase database configured with the expected tables
- Android build support in Unity if you plan to build the mobile app

## Unity Setup

1. Open the repository root in Unity Hub.
2. Use Unity Editor `6000.2.10f1` when prompted.
3. Let Unity restore packages from `Packages/manifest.json`.
4. Verify the local MediaPipe package exists at `LocalPackages/MediaPipeUnityPlugin-all/`.
5. Open the main scene and confirm any required scene references are assigned in the Inspector.

### Unity Configuration Notes

- The Unity app uses `SupabaseManager` for authentication, database access, and realtime subscriptions.
- `ASLManager` calls a hosted recognition API to classify sign input.
- Android text-to-speech is used on device; in the Unity Editor this falls back to debug logging.

## Admin Portal Setup

1. Go to `Kumpas.AdminWeb/`.
2. Review `appsettings.json` and `appsettings.Development.json`.
3. Set the database connection string and Supabase settings for your environment.
4. Run the app:

```powershell
dotnet run --project Kumpas.AdminWeb
```

5. Open the local URL printed by ASP.NET Core in your browser.

### Admin Portal Configuration

The admin portal expects:

- `ConnectionStrings:DefaultConnection`
- `Supabase:Url`
- `Supabase:AnonKey`
- `Supabase:ServiceRoleKey`

For team use, prefer storing real credentials in development overrides, environment variables, or secrets management instead of committing environment-specific values.

## Notes for Contributors

- This repository may contain generated Unity files and local package sources; avoid broad cleanup unless it is intentional.
- The current working tree may include unrelated Unity setting changes during normal editor use.
- If you add more setup steps, keep this README aligned with both the Unity app and the admin portal.

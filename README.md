# SelfPin

A lightweight, self-hosted, privacy-focused location sharing application designed for families and private groups.
The goal of SelfPin is to give ownership of location data without relying on cloud services or privacy-invading account signups.

---

## Project Overview

**SelfPin** is built as a self-hosted alternative to proprietary location-sharing services. It consists of:

- **Backend API Server:** Built with **.NET 10**, **Entity Framework Core**, and **SQLite**. It handles simple device authentication via API tokens, processes background location updates, and manages user state.
- **Mobile Client:** Built with **Vue 3**, **Vite**, and **Capacitor**. It tracks device location in the background and sends periodic updates to your self-hosted server.
- **Web Interface / Map:** Utilizes **Leaflet.js** and **OpenStreetMap** to present clean, real-time map visualization without requiring third-party map API keys.

---

## Prerequisites

Before getting started, ensure you have the following installed on your system:

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js (v18+)](https://nodejs.org/) & `npm`
- [Android Studio](https://developer.android.com/studio) [for Android builds]
- [Xcode](https://developer.apple.com/xcode/) [macOS only, for iOS builds]

---

## Building & Running the Application

### 1. Repository Structure

```code
self-pin/
  ├── server/          # .NET 10 Web API (Backend & SQLite DB)
  ├── client/          # Vue 3 + Vite + Capacitor (Mobile & Web UI)
  └── README.md
```

---

### 2. Backend Server (.NET 10 API)

#### Setup & Run

1. Navigate to the server project folder:
   `cd server`
2. Restore dependencies and run the server:
    `dotnet run` \
    *The server will start locally at <http://localhost:5180> (or <https://localhost:7078>). On initial run, a `location.db` SQLite database file is created automatically.*

#### Create a Test Device

Register a device to generate an authentication token:

```shell
curl -X POST <http://localhost:5180/admin/users> \
  -H "Content-Type: application/json" \
  -d '{"name": "Family Member"}'
```

*Save the returned deviceToken to configure your mobile client.*

---

### 3. Client Application (Vue 3 + Capacitor)

#### Setup & Local Development

1. Navigate to the client directory:
   `cd client`
2. Install npm dependencies:
   `npm install`
3. Run the development web server:
   `npm run dev`

#### Building for Mobile Devices

1. Update your device token and backend server URL in `src/App.vue` or your configuration service.
2. Build the production Web app and sync native platforms:

   ## Build the Vue Web app

   `npm run build`

   ## Sync build artifacts into iOS and Android projects

   `npx cap sync`
3. Open the native projects in their respective IDEs to deploy to a device or emulator:
   - Android:
     `npx cap open android`
   - iOS (macOS only):
     `npx cap open ios`

---

## AI Disclosure

This project was developed with the assistance of **Google Gemini**, which is used as an AI coding assistant.

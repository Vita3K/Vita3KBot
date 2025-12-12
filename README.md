# Vita3KBot

[![Vita3K Discord Server](https://img.shields.io/discord/408916678911459329?color=5865F2\&label=Vita3K\&logo=discord\&logoColor=white)](https://discord.gg/6aGwQzh)
[![Build Vita3KBot](https://github.com/Vita3K/Vita3KBot/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/Vita3K/Vita3KBot/actions/workflows/build.yml)

A Discord bot for the official **Vita3K** Discord server.

---

## Features

* Provides automated utilities and information for the Vita3K community
* Designed to integrate seamlessly with the Vita3K Discord server

---

## Development Requirements

To build and develop Vita3K-Bot, you will need:

* **.NET 8.0 SDK** or newer
  [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

* A text editor or IDE (recommended options):

  * **Visual Studio** (Windows, free Community edition available)
  * **Visual Studio Code** (cross-platform, free)

---

## Runtime Requirements

* **.NET 8.0 SDK** or newer (required when running from source)

---

## How to Build

1. Open the solution file:

   ```
   Vita3K-Bot.sln
   ```

2. Build the solution using your IDE:

   ```
   Menu → Build Solution (Ctrl + Shift + B)
   ```

3. If the build succeeds, the executable will be generated in one of the following directories:

   ```
   bin/Debug/netX/
   bin/Release/netX/
   ```

---

## How to Run

1. Open the **Discord Developer Portal**:
   [https://discord.com/developers/applications](https://discord.com/developers/applications)

2. Create a new **Application** and add a **Bot** to it.

3. Copy the bot token and save it into a file named:

   ```
   token.txt
   ```

   Place this file in the bot’s working directory.

4. Grant the required permissions to the bot and invite it to your Discord server.

5. Run the executable generated during the build step.

---

## Notes

* Keep your `token.txt` private and **never commit it to version control**.

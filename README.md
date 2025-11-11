# Fergun
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE) [![Discord](https://discord.com/api/guilds/460627183501574144/widget.png)](https://discord.gg/V3TgaZRUPX)

Fergun is a utility bot/application with lots of useful commands.

You can invite Fergun to your Discord server clicking [here](https://discord.com/oauth2/authorize?client_id=680507783359365121&scope=bot%20applications.commands).

Have any questions or need help with the bot? Join the [support server](https://discord.gg/V3TgaZRUPX).

## Features
- Translate text from and to more than 140 languages using a robust translation module that is powered by popular translators like Google Translate, Microsoft Translator and Yandex Translate
- Image search from Google and DuckDuckGo
- Reverse image search using Google, Bing and Yandex
- Perform OCR on images using Google, Bing and Yandex
- Perform text-to-speech using Google and Microsoft Azure
- Translate a text multiple times using different translators for bad translations
- Get complete definitions of words from Dictionary.com
- Get complete answers to your queries using Wolfram Alpha
- Get inspirational quotes from InspiroBot
- Search for or get random definitions from Urban Dictionary
- Search for Wikipedia articles
- Search for song lyrics from Genius
- Get the lyrics of the song you're listening to on Spotify
- Search YouTube videos
- Get user information and server/global/default avatar
- Generate images of colors
- 2 supported languages (English and Spanish)
- Support for localized output (like localized results) in most commands
- And more coming soon™️

## Setup

### 0. Prerequisites
* A Discord bot application (You can create one [here](https://discord.com/developers/applications)).
* [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 1. Build and run the bot
* Clone the repository:
  `git clone https://github.com/d4n3436/Fergun.git`
  
* Build the bot:
  ```
  cd Fergun
  dotnet build -c Release
  ```
  
* Go to the build output folder: 
  ```
  cd src/bin/Release/net10.0
  ```
  
* Open `appsettings.json` with a text editor and set the application token:
  ```json
  {
      "Startup":
      {
          "Token": "put your token here"
      }
  }
  ```
  
* Start the bot by double clicking `Fergun.exe` or with the command `dotnet Fergun.dll`.

  The application should automatically create the SQLite database and start the bot.

* To start using Fergun, simply type `/` in a server with the bot and use its commands.

## Contributing
Feel free to report bugs or request new features via issues or pull requests. Requesting new commands may or may not be accepted depending on the utility and usability of that command.

## License
Fergun is licensed under the [MIT license](LICENSE).

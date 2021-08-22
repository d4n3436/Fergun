# Fergun
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE) [![Discord](https://discord.com/api/guilds/460627183501574144/widget.png)](https://discord.gg/5w5GEKE)

Fergun is a multipurpose and multilanguage bot with lots of useful commands (Utility, Music, Moderation, and AI Dungeon).

You can invite Fergun to your Discord server clicking [here](https://discord.com/oauth2/authorize?client_id=680507783359365121&scope=bot&permissions=204860486).

Have any questions or need help with the bot? Join the [support server](https://discord.gg/5w5GEKE).

## Supported Languages
| Language | Human Translation |
| --       | --  |
| English  | ✅ |
| Spanish  | ✅ |
| Arabic   | ❌ |
| Turkish  | ❌ |
| Russian  | ❌ |


## Setup
### 0. Prerequisites
* A Discord bot application (You can create one [here](https://discord.com/developers/applications)).

* [.NET 5 SDK](https://dotnet.microsoft.com/download)

* A MongoDB server (You can get a Free Tier cluster [here](https://docs.atlas.mongodb.com/tutorial/deploy-free-tier-cluster/) or install the system to your local machine [here](https://docs.mongodb.com/manual/administration/install-community/)).


### 1. Building the bot
* Clone  the repository:
  `git clone https://github.com/d4n3436/Fergun.git`

  Or [download from GitHub](https://github.com/d4n3436/Fergun/archive/master.zip).

* Build the bot (change `Release`/`ReleaseLabs` to `Debug`/`DebugLabs`<sup id="7-interactions-support">[note](#f1)</sup> in a debug build):
  ```
  cd Fergun
  dotnet build -c Release
  ```

### 2. Setting up a local Lavalink server (Optional)
If you want to use the music module with a local Lavalink server, follow these steps:
*  [Install JDK 11+](https://www.oracle.com/java/technologies/javase-jdk11-downloads.html)

* Create a folder in the build folder called "Lavalink" (change `Release`/`ReleaseLabs` to `Debug`/`DebugLabs`<sup id="7-interactions-support">[note](#f1)</sup> in a debug build):
  ```
  cd src/bin/Release/net5.0
  mkdir Lavalink
  ```
  
* Download the [Lavalink binaries](https://github.com/freyacodes/Lavalink/releases/latest/download/Lavalink.jar) and save it in the folder:
 
  `wget https://github.com/freyacodes/Lavalink/releases/latest/download/Lavalink.jar -O Lavalink.jar`
 
  [Nightly binaries](https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/Lavalink.jar?guest=1):
 
  `wget https://ci.fredboat.com/repository/download/Lavalink_Build/lastSuccessful/Lavalink.jar?guest=1 -O Lavalink.jar`
 
 * Download [application.yml](https://raw.githubusercontent.com/freyacodes/Lavalink/master/LavalinkServer/application.yml.example%20-O%20application.yml) and save it in the folder:
 
   `wget https://raw.githubusercontent.com/freyacodes/Lavalink/master/LavalinkServer/application.yml.example -O application.yml`
 
   Be sure to save it as `application.yml` and not `application.yml.example`.


### 3. Running the bot
* Go to the build folder if you haven't done it before (change `Release`/`ReleaseLabs` to `Debug`/`DebugLabs`<sup id="7-interactions-support">[note](#f1)</sup> in a debug build):
 
  `cd src/bin/Release/net5.0`
 
* Start the bot by double clicking `Fergun.exe` or with the command `dotnet Fergun.dll`.

* You will see the error message: "No config file found. Creating default config file." The error is self explanatory.

* In the build folder, open the file `botconfig.json` with a text editor.

* Copy your bot token and paste it in the `Token` or `DevToken` field (Release or Debug build).

* Fill the database login information in `DatabaseConfig` (If you're using a local database and no authentication then you don't have to change anything).

* If you're using a remote Lavalink server you may also want to change the `Hostname` and `Authorization` fields in `LavaConfig`.

* Start the bot again, now the bot should be running with the minimal config.

Note: If you set up a local Lavalink server the bot should be running the server automatically.


### 4. Testing and changing the default prefix
The default bot prefix is `f!` (`f!!` in Debug builds), a `@mention` can also be used as a prefix.

To test the bot use the `ping` command: `f!ping`.
You should see an embed with the response times.

To change the default (global) bot prefix use `globalprefix <newPrefix>`: `f!globalprefix !`.
This will set the global prefix to `!` and save it in the database.

To change the prefix in the current server simply use `prefix <newPrefix>`.

To change the language in the current server use `language`.

To shut down the bot use `logout`.


### 5. More configuration
`botconfig.json` documentation:

| Key | Description | How to get one / Notes
|--|--|--|
| `Token` | The bot token. | [Create a Discord application](https://discord.com/developers/applications).
| `DevToken` | The development bot token, used in Debug builds. | ^
| `TopGgApiToken` | The top.gg API token, used to update the bot server count in top.gg. | [Add a bot in top.gg](https://top.gg/bot/new), then [here](https://top.gg/api/docs).
| `DiscordBotsApiToken` | The Discord Bots API token, used to update the bot server count in discord.bots.gg. | [Add a bot in discord.bots.gg](https://discord.bots.gg/bots/add), then [here](https://discord.bots.gg/docs).
| `GeniusApiToken` | The Genius API token, used in the commands `lyrics` and `spotify`. | https://docs.genius.com
| `AiDungeonToken` | The AI Dungeon user token, used in the AI Dungeon module. | See [below](#6-obtaining-the-ai-dungeon-token).
| `DeepAiApiKey` | The DeepAI API key, used in `resize`. | https://deepai.org/api-docs
| `ApiFlashAccessKey` | The ApiFlash access key, used in `screenshot` and `archive`. | https://apiflash.com
| `WolframAlphaAppId` | The WolframAlpha App ID, used in `wolframalpha`. | https://products.wolframalpha.com/api
| `EmbedColor` | The raw value of the color the bot will use in its embeds. | The default value is 16750877 or orange :)
| `SupportServer` | The support server invite. | Get a server invite.
| `LogChannel` | The ID of the channel the bot will send error logs. | Create a text channel and copy the ID.
| `PresenceIntent` | Whether the Guild Presences intent should be used. Used in multiple commands, required in `spotify`. | If your bot is in more than 100 servers this requires [verification and whitelisting](https://support.discord.com/hc/en-us/articles/360040720412).
| `ServerMembersIntent` | Whether the Guild Members intent should be used. Used in user join/leave/kick events and for downloading the entire member list. | If your bot is in more than 100 servers this requires [verification and whitelisting](https://support.discord.com/hc/en-us/articles/360040720412)
| `MessageCacheSize` | The message cache size, used in commands that gets cached messages in a channel. | The default value is 100, setting this to 0 disables the message cache.
| `MessagesToSearchLimit` | The number of messages to search in a channel. | This is used in commands that searches for a Url in the messages of a channel.
| `AlwaysDownloadUsers` | Whether all users should be downloaded to the cache. | `ServerMembersIntent` is required for this to work.
| `UseReliabilityService` | Whether the reliability service should be used. | The reliability service is a service that shutdowns the bot in case of a deadlock.<br/>The service requires that the bot is being run by a daemon that handles Exit Code 1 as a restart.<br/>Daemon for [Powershell](https://gitlab.com/snippets/21444) and [Bash](https://stackoverflow.com/a/697064).
| `UseCommandCacheService` | Whether the command cache service should be used. | The command cache service is a service that tracks command (user) messages and the bot response messages.<br/>When a command message is modified or deleted, the bot will also modify or delete the corresponding response message automatically.
| `UseMessageCacheService` | Whether the message cache service should be used. | The command message service is a service that stores deleted and modified messages temporarily.<br/>This service is used in the "snipe" commands.
| `DonationUrl` | The donation Url. | ...
| `DatabaseConfig` | The database configuration. | ...
| `LavaConfig` | The Lavalink server configuration | ...
| `(...)Emote` | The emotes that are used in some commands. | `LoadingEmote` is used in a "Loading" message.<br/>`MongoDbEmote` and `WebSocketEmote` are used in `ping`.<br/>`BoosterEmote` and `UserFlagsEmotes` are used in `userinfo`.<br/>The rest are used in `serverinfo`.

### 6. Obtaining the AI Dungeon token

(These steps may differ depending on what web browser you're using. Here I'll use Google Chrome.)

* Go to https://play.aidungeon.io in your web browser and log in.
* Open the Developer tools (press F12).
* Go to the `Network` tab and click the `WS` button.
* Press F5. A `subscriptions` connection should appear.
* Click it, go to the `Messages` tab and scroll up to the first message.
* Click the first message. The token is the value of the `token` key.

### 7. Interactions support
<b id="f1"></b>
Fergun now supports interactions with Discord.Net Labs. It is currently used to replace reactions with buttons/select menus in paginators and selections. To be able to use interactions you will need to build the bot using either the `ReleaseLabs` or the `DebugLabs` configuration.

## Contributing
Feel free to report bugs or request new features via issues or pull requests. Requesting new commands may or may not be accepted depending on the utility and usability of that command.

## License
Fergun is licensed under the [MIT license](LICENSE).

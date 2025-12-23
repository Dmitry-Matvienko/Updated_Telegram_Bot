# MyUpdatedTelegramBot

**Updated_Telegram_Bot** — is an updated version of the Telegram-chat-bot, completely rewritten from scratch in .NET 8 using modern approaches (DI, hosting, background services, EF Core, Serilog, etc.).

## Key features

- **User registration** upon any first message
- **Message counting** (MessageStatsService) with buffering and periodic DB recording
- **Ranking system** based on the number of messages
- **Reputation system** (in `ReplyToMessage`) with flood protection
- **Local(in a group) and global top-10** by messages and raputation
- Flexible **architecture**
- Logging via **Serilog** (configuration via `appsettings.json` + reloadOnChange)
- **Tools for owner** for user statistics and sending messages in every chat
- Popular **Crocodile game** for groups. List of words are in  `crocodile-words.txt`
- **RollGame** is a quick game `roll the dice` (value 1–100). The result is recorded and displayed in the leaderboard.
- **User reports** — users can send complaints to admins using the commands `!админ` or `!report`. The admin receives the complaint and processes it

## Project structure

```
/ Core          # basic models, interfaces, handlers
  / Handlers    # ICommandHandler for bot commands
  / Models      # EF Core entities and DTOs
/ Infrastructure # hosting, BotHostedService, Data (MyDbContext)
/ Services      # different services, including background services (MessageStatsService, RatingService)
Program.cs      # Main point, host configuration
appsettings.json# project configuration

Migrations/     # EF Core migrations
```

## Requirements

- .NET 8 SDK
- SQL Server (LocalDB or full version)
- `dotnet-ef` for working with migrations

## Setup and launch

1. Clone the repository:
   ```bash
   git clone https://github.com/Dmitry-Matvienko/Updated_Telegram_Bot.git
   cd UpdatedTelegramBot
   ```

2. Configure secrets (in Development):
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "TelegramBot:Token" "<your-bot-token>"
   dotnet user-secrets set "Admin:Ids:0" "<Owner id(s)>"
   ```
   Or set an environment variable:
   - Windows PowerShell:
     ```powershell
     $Env:TelegramBot__Token = "<your-bot-token>"
     ```
	 
	 ```powershell
	 $Env:Admin__Ids__0 = "123456789"
	 ```
   - Linux/macOS Bash:
     ```bash
     export TelegramBot__Token= "<your-bot-token>"
     ```
	 
	 ```bash
     export Admin__Ids__0 = "123456789"
     ```

3. Set the connection string in `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyUpdatedBotDb;Trusted_Connection=True;"
   }
   ```

4. Add migration and update the DB:
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

5. Launch the bot:
   ```bash
   dotnet run
   ```

## Basic bot commands

- `/LocalMessageRate` — local top-10 by number of messages
- `/GlobalMessageRate` — global top-10 by number of messages
- `/LocalRating` — local top-10 by “thank you” rating
- `/GlobalRating` — global top-10 in the “thank you” rating
- **Reply to user** «спасибо» or «благодарю» — give the user a +1 rating (with flood protection)
- `/CrocodileGame` — the game “crocodile” where users have to guess the word
- `/StartRoll` — a “roll the dice” game with values from 1 to 100 and a leaderboard
- **Reply to user** «!админ» or «!report» to send a complaint to the admins

## Architecture

- **Startup / DI (Program.cs)**: initialization of configuration, logging, and registration of services/handlers
- **BotHostedService**: starts polling and delegates updates to `UpdateDispatcher` via DI-scoped `IUpdateHandlerService`.
- **UpdateDispatcher**: iterates through all `ICommandHandler`s, calls `CanHandle` + `HandleAsync`.
- **MessageCountStatsService**: background service with `Channel<>`, batching `MessageCountEntity`.
- **UserLeaderboard**: service responsible for generating local and global top-10 lists based on messages and ratings
- **EF Core**: `MyDbContext` with `DbSet<UserEntity>`, `MessageStats`, `RatingStats`.
- **Serilog**: configured via `appsettings.json` + `UseSerilog(...).ReadFrom.Configuration(..., reloadOnChange:true)`.

## Logging

- Default leve — `Information`.
- For debugging, you can raise it to `Debug` or `Trace` in `appsettings.json` without restarting.
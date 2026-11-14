# SkillShelf

A local AI Skills Marketplace where team members can publish a skill ZIP, browse and search the collection, preview its instructions, and download the original package.

## Run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

From the folder containing `SkillsMarketplace.csproj`:

```powershell
dotnet restore
dotnet run --urls http://localhost:5050
```

Open http://localhost:5050. Stop with Ctrl+C.

Internet access is needed for the first package restore. No separate database server is required.

## Try it

Three sample skills appear automatically: **Review a code change**, **Plan meaningful tests**, and **Explain a codebase**.

1. Search the collection and select **View skill** to read a skill’s instructions.
2. Select **Download ZIP** on a card to download its package.
3. Select **Publish a skill**, enter a name, description, and author, and upload a skill ZIP.
4. Stop and restart the app to confirm that published skills persist and samples are not duplicated.

Sample ZIPs and their editable source folders are included in `samples/`.

## Decisions and tradeoffs

- **C# / ASP.NET Core Razor Pages:** keeps the UI and backend in one application, simplifying development and local setup.
- **SQLite:** stores skill metadata in a local database file without requiring a separate database server. Parameterized queries support name and description searches.
- **Original ZIP storage:** keeps packages outside the public web root using generated filenames. Downloads return the original uploaded bytes.
- **Package validation:** enforces size and structure limits and rejects unsafe paths and invalid packages. Contents are never extracted or executed, and instructions are displayed as plain text.
- **Simple UI:** server-rendered forms and plain CSS provide a responsive interface without a separate JavaScript build.
- **Startup samples:** three usable examples make the marketplace immediately explorable. Fixed IDs prevent duplicate samples on restart and preserve existing uploads.

Persistent data is created automatically in `App_Data/`.

## Verification

Build the application and run the automated checks:

```powershell
dotnet build
dotnet run --project tests/SkillsMarketplace.Checks.csproj
```

All 24 automated checks passed on Windows. Manual testing confirmed publishing, search, instruction previews, downloads, persistence, and no duplicate samples after restart.

## AI assistance

I used ChatGPT/Codex to explore implementation approaches and generate code and tests. I guided the feature set and UI design, then iterated through local builds, automated checks, and manual testing.

## With more time

- Add authentication and verified authorship for shared use.
- Support skill versions and duplicate upload detection.
- Add pagination and improve search.
- Expand automated testing to cover complete browser workflows.
- Improve storage limits and recovery from interrupted uploads.

## Code map

- `Program.cs`: register services, initialize storage, route requests.
- `Models/Skill.cs`: one published skill record.
- `Services/PackageValidator.cs`: inspect ZIPs and bound their contents.
- `Services/SkillRepository.cs`: query SQLite and save original ZIP bytes.
- `Services/SampleSeeder.cs`: add missing bundled examples at startup.
- `Pages/Index.*`: search and browse.
- `Pages/Publish.*`: validate the form and publish.
- `Pages/Details.*`: preview instructions and download.
- `Pages/Shared/_Layout.cshtml`, `wwwroot/css/site.css`: shared shell and styling.
- `tests/Program.cs`: executable behavior checks.
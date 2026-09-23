# MoonDex

A Unity project.

## Getting Started

### Prerequisites
- Unity [Version Number]
- [Any other dependencies]

### Setup
1. Clone the repository
2. Open the project in Unity
3. [Additional setup steps]

## Development

### Project Structure
- `Assets/` - Main project assets
- `ProjectSettings/` - Unity project settings
- `Packages/` - Package dependencies

### Building

#### WebGL / GitHub Pages

The Pages workflow publishes a locally exported WebGL build. It does not run
Unity in CI or require Unity license credentials.

1. In GitHub repository **Settings > Pages**, set **Source** to **GitHub Actions**.
2. In Unity's Build Profiles, select **Web**, with `Assets/Scenes/MoonDex.unity`
   enabled. In Player Settings > Publishing Settings, use **Brotli** compression
   with **Decompression Fallback** enabled, and leave native C/C++ multithreading
   disabled. The fallback allows compressed files to load on GitHub Pages without
   custom server headers.
3. Export to `Build/Web/` at the repository root. `Build/Web/index.html`, `Build/Web/Build/`,
   `Build/Web/TemplateData/`, and any generated `StreamingAssets/` must remain together.
   Replace the previous export when rebuilding so obsolete payloads do not accumulate.
   Unity regenerates the HTML and stylesheet on export; preserve the tracked page
   styling when updating the build payload.
4. Commit the complete export and push it to `master`. The **Publish WebGL to
   GitHub Pages** workflow deploys it automatically. It can also be run manually
   from the Actions tab after an export is committed.

Once deployed, the site is at <https://digimbyte.github.io/moon-dex/>.
Only `Build/Web/` is uploaded; project source and repository documentation are excluded.
The workflow reports an error if the export's `index.html` is missing.

See [GitHub Pages workflow setup](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)
and [Unity Web deployment settings](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/deploying).

## Contributing
[Contribution guidelines]

## License
[License information]

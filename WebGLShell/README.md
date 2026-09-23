# MoonDex WebGL shell

Edit the repository-root `index.html` and `WebGLShell/style.css` to change the
live page. Serve the repository root over HTTP to open it locally.

The page loads Unity content directly from `Build/Web/Build` and
`Build/Web/StreamingAssets`. Keep exporting to `Build/Web` with the current
`Web.*` filenames and decompression fallback enabled. Unity's generated
`Build/Web/index.html` is not used.

GitHub Pages packages the root page, stylesheet, and exported Unity content.
Rebuilding Unity cannot overwrite the permanent page or stylesheet.

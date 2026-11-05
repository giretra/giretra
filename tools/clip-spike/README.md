# Clip spike renderer (phase 0)

Local renderer kept from `docs/plans/shareable-clips.md` phase 0. Drives a replay page
(`/replay/match/{matchId}/{deal}?render=1`) frame by frame with Playwright, pipes JPEG frames
into ffmpeg and compares the frame hashes of two runs. In `--offline` mode any match id
returns a deterministic demo deal.

```bash
# terminal 1 / 2: backend offline + Angular dev server
dotnet run --project src/Giretra.Web -- --offline
cd src/Giretra.Web/ClientApp/giretra-web && npm start

# render twice at 30 fps, 2x scale, JPEG frames, into ./out
cd tools/clip-spike && FORMAT=jpeg dotnet run -c Release -- \
  'http://localhost:4200/replay/match/00000000-0000-0000-0000-000000000001/1?render=1&view=Bottom' ./out 2 30 2
```

Args: page URL, output dir, runs, fps, device scale factor. Env: `FORMAT=png|jpeg`,
`QUALITY` (JPEG, default 90), `CHROME` (default `/usr/bin/google-chrome`).
Not part of the solution; it is kept as the manual "render one clip locally" tool.

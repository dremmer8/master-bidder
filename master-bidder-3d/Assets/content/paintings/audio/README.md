# Modular painting voiceovers (ElevenLabs)

Clips are stored **by information type**, not one file per painting.

```
audio/
  genre/<slug>.mp3      # shared (~10)
  period/<slug>.mp3     # shared (~13)
  artist/<slug>.mp3     # shared (~63)
  year/<slug>.mp3       # shared (~85)
  title/<artworkId>.mp3 # per painting (~90)
  fact/<artworkId>.mp3  # per painting (~90)
  PaintingVoiceoverLibrary.asset
  manifest.json
```

Generate in Unity: **Master Bidder → Painting Voiceovers (ElevenLabs)**  
Or CLI: `cd mvp && npm run voiceovers`

After generate, click **Rebuild Library Only**, then assign `PaintingVoiceoverLibrary` on `AppFlow` / `AudioManager`.
